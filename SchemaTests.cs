using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Dasher;
using Xunit;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaComparisons
{
    // TODO reflect integral conversions supported by dasher
    // TODO test writing empty message to complex with all-default values

    public enum EnumAbc { A, B, C }
    public enum EnumAbcd { A, B, C, D }

    public class Person
    {
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; }
        public int Age { get; }
    }

    public class PersonWithScore
    {
        public PersonWithScore(string name, int age, double score)
        {
            Name = name;
            Age = age;
            Score = score;
        }

        public string Name { get; }
        public int Age { get; }
        public double Score { get; }
    }

    public class PersonWithDefaultScore
    {
        public PersonWithDefaultScore(string name, int age, double score = 100.0)
        {
            Name = name;
            Age = age;
            Score = score;
        }

        public string Name { get; }
        public int Age { get; }
        public double Score { get; }
    }

    public class PersonWithDefaultHeight
    {
        public PersonWithDefaultHeight(string name, int age, double height = double.NaN)
        {
            Name = name;
            Age = age;
            Height = height;
        }

        public string Name { get; }
        public int Age { get; }
        public double Height { get; }
    }

    public class SchemaTests
    {
        /// <summary>Required as Dasher won't serialise non-complex top-level types.</summary>
        public class Wrapper<T>
        {
            public T Value { get; }

            public Wrapper(T value)
            {
                Value = value;
            }
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
        private static IEnumerable<TRead> Test<TWrite, TRead>(TWrite write, TRead read, bool matchIfRelaxed, bool matchIfStrict)
        {
            var schemaCollection = new SchemaCollection();

            var w = schemaCollection.GetWriteSchema(typeof(TWrite));
            var r = schemaCollection.GetReadSchema(typeof(TRead));

            var actualMatchIfRelaxed = r.CanReadFrom(w, allowWideningConversion: true);
            var actualMatchIfStrict = r.CanReadFrom(w, allowWideningConversion: false);

            Assert.Equal(matchIfRelaxed, actualMatchIfRelaxed);
            Assert.Equal(matchIfStrict,  actualMatchIfStrict);

            if (!actualMatchIfRelaxed && !actualMatchIfStrict)
                yield break;

            var stream = new MemoryStream();

            new Serialiser<Wrapper<TWrite>>().Serialise(stream, new Wrapper<TWrite>(write));

            if (actualMatchIfRelaxed)
            {
                stream.Position = 0;
                yield return new Deserialiser<Wrapper<TRead>>(UnexpectedFieldBehaviour.Ignore).Deserialise(stream).Value;
            }

            if (actualMatchIfStrict)
            {
                stream.Position = 0;
                yield return new Deserialiser<Wrapper<TRead>>(UnexpectedFieldBehaviour.Throw).Deserialise(stream).Value;
            }
        }

        [Fact]
        public void ComplexTypes_FieldsMatch()
        {
            var read = Test(
                new Person("Bob", 36),
                new Person("Bob", 36),
                matchIfRelaxed: true,
                matchIfStrict: true);

            foreach (var person in read)
            {
                Assert.Equal("Bob", person.Name);
                Assert.Equal(36, person.Age);
            }
        }

        [Fact]
        public void ComplexTypes_ExtraField()
        {
            var read = Test(
                new PersonWithScore("Bob", 36, 100.0),
                new Person("Bob", 36),
                matchIfRelaxed: true,
                matchIfStrict: false);

            foreach (var person in read)
            {
                Assert.Equal("Bob", person.Name);
                Assert.Equal(36, person.Age);
            }
        }

        [Fact]
        public void ComplexTypes_InsufficientFields()
        {
            // ReSharper disable once IteratorMethodResultIsIgnored
            Test(
                new Person("Bob", 36),
                new PersonWithScore("Bob", 36, 100.0),
                matchIfRelaxed: false,
                matchIfStrict: false);
        }

        [Fact]
        public void ComplexTypes_MissingNonRequiredField_AtLexicographicalEnd()
        {
            var read = Test(
                new Person("Bob", 36),
                new PersonWithDefaultScore("Bob", 36),
                matchIfRelaxed: true,
                matchIfStrict: true);

            foreach (var person in read)
            {
                Assert.Equal("Bob", person.Name);
                Assert.Equal(36, person.Age);
                Assert.Equal(100.0, person.Score);
            }
        }

        [Fact]
        public void ComplexTypes_MissingNonRequiredField_InLexicographicalMiddle()
        {
            var read = Test(
                new Person("Bob", 36),
                new PersonWithDefaultHeight("Bob", 36),
                matchIfRelaxed: true,
                matchIfStrict: true);

            foreach (var person in read)
            {
                Assert.Equal("Bob", person.Name);
                Assert.Equal(36, person.Age);
                Assert.Equal(double.NaN, person.Height);
            }
        }

        [Fact]
        public void Enum_MembersMatch()
        {
            var read = Test(
                EnumAbc.A,
                EnumAbc.A,
                matchIfRelaxed: true,
                matchIfStrict: true);

            foreach (var e in read)
                Assert.Equal(EnumAbc.A, e);
        }

        [Fact]
        public void Enum_ExtraMember()
        {
            var read = Test(
                EnumAbc.A,
                EnumAbcd.A,
                matchIfRelaxed: true,
                matchIfStrict: false);

            foreach (var e in read)
                Assert.Equal(EnumAbcd.A, e);
        }

        [Fact]
        public void Enum_InsufficientMembers()
        {
            // ReSharper disable once IteratorMethodResultIsIgnored
            Test(
                EnumAbcd.A,
                EnumAbc.A,
                matchIfRelaxed: false,
                matchIfStrict: false);
        }

        [Fact]
        public void EmptySchema_ExactMatch()
        {
            var read = Test<EmptyMessage, EmptyMessage>(
                null,
                null,
                matchIfRelaxed: true,
                matchIfStrict: true);

            foreach (var v in read)
                Assert.Null(v);
        }

        [Fact]
        public void EmptySchema_Complex()
        {
            var read = Test<Person, EmptyMessage>(
                new Person("Bob", 36),
                null,
                matchIfRelaxed: true,
                matchIfStrict: false);

            foreach (var v in read)
                Assert.Null(v);
        }

        [Fact]
        public void EmptySchema_Union()
        {
            var read = Test<Union<int, string>, EmptyMessage>(
                1,
                null,
                matchIfRelaxed: true,
                matchIfStrict: false);

            foreach (var v in read)
                Assert.Null(v);
        }

        [Fact]
        public void UnionSchema_ExactMatch()
        {
            var read = Test<Union<int, string>, Union<int, string>>(
                1,
                1,
                matchIfRelaxed: true,
                matchIfStrict: true);

            foreach (var v in read)
                Assert.Equal(1, v);
        }

        [Fact]
        public void UnionSchema_ExtraMember()
        {
            // ReSharper disable once IteratorMethodResultIsIgnored
            Test<Union<int, string, double>, Union<int, string>>(
                1,
                1,
                matchIfRelaxed: false,
                matchIfStrict: false);
        }

        [Fact]
        public void UnionSchema_FewerMembers()
        {
            var read = Test<Union<int, string>, Union<int, string, double>>(
                1,
                1,
                matchIfRelaxed: true,
                matchIfStrict: false);

            foreach (var v in read)
                Assert.Equal(1, v);
        }

        [Fact]
        public void ListSchema_SameType()
        {
            var read = Test<IReadOnlyList<int>, IReadOnlyList<int>>(
                new[] {1, 2, 3},
                new[] {1, 2, 3},
                matchIfRelaxed: true,
                matchIfStrict: true);

            foreach (var list in read)
                Assert.Equal(new[] {1, 2, 3}, list);
        }

        [Fact]
        public void ListSchema_CompatibleIfRelaxed()
        {
            var read = Test<IReadOnlyList<PersonWithScore>, IReadOnlyList<Person>>(
                new[] {new PersonWithScore("Bob", 36, 100.0) },
                new[] {new Person("Bob", 36) },
                matchIfRelaxed: true,
                matchIfStrict: false);

            foreach (var list in read)
            {
                foreach (var person in list)
                {
                    Assert.Equal("Bob", person.Name);
                    Assert.Equal(36, person.Age);
                }
            }
        }

        [Fact]
        public void ListSchema_IncompatibleTypes()
        {
            // ReSharper disable once IteratorMethodResultIsIgnored
            Test<IReadOnlyList<int>, IReadOnlyList<string>>(
                new[] {1, 2, 3},
                new[] {"1", "2", "3"},
                matchIfRelaxed: false,
                matchIfStrict: false);
        }
    }
}
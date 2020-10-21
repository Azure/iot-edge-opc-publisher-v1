// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using OpcPublisher.Configurations;
using System.Collections.Generic;
using Xunit;

namespace OpcPublisher
{
    public class ProgramMethodTests
    {
        [Fact]
        [Trait("Options", "MethodTest")]
        public void ParseListOfStringsOk()
        {
            string testString = "a, b, c, d";
            List<string> resultOk = new List<string>();
            resultOk.Add("a");
            resultOk.Add("b");
            resultOk.Add("c");
            resultOk.Add("d");

            List<string> result = new List<string>();
            result = CommandLineArgumentsParser.ParseListOfStrings(testString);
            Assert.Equal(resultOk, CommandLineArgumentsParser.ParseListOfStrings(testString));
        }


        [Theory]
        [Trait("Options", "MethodTest")]
        [MemberData(nameof(ParseListOfStringsSimplePass0))]
        public void ParseListOfStringsPass(string test, List<string> expected)
        {
            Assert.Equal(expected, CommandLineArgumentsParser.ParseListOfStrings(test));
        }

        public static IEnumerable<object[]> ParseListOfStringsSimplePass0 =>
            new List<object[]>
            {
                new object[] {
                    // test
                    "this, is, my, normal, test, string",
                    // expected
                    new List<string>
                        {   new string("this"),
                            new string("is"),
                            new string("my"),
                            new string("normal"),
                            new string("test"),
                            new string("string"),
                        },
                },
                new object[] {
                    // test
                    "this, is, my, nor mal, test, string",
                    // expected
                    new List<string>
                        {   new string("this"),
                            new string("is"),
                            new string("my"),
                            new string("nor mal"),
                            new string("test"),
                            new string("string"),
                        },
                },
                new object[] {
                    // test
                    "\"this, is, my, normal, test, string\"",
                    // expected
                    new List<string>
                        {   new string("this, is, my, normal, test, string"),
                        },
                },
                new object[] {
                    // test
                    "\"this, is\", \"my, normal\", \"test, string\"",
                    // expected
                    new List<string>
                        {   new string("this, is"),
                            new string("my, normal"),
                            new string("test, string"),
                        },
                },
            };
    }
}

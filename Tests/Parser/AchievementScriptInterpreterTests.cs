﻿using Jamiras.Components;
using NUnit.Framework;
using RATools.Data;
using RATools.Parser;
using System.Linq;

namespace RATools.Test.Parser
{
    [TestFixture]
    class AchievementScriptInterpreterTests
    {
        private AchievementScriptInterpreter Parse(string input, bool expectedSuccess = true)
        {
            var tokenizer = Tokenizer.CreateTokenizer(input);
            var parser = new AchievementScriptInterpreter();

            if (expectedSuccess)
            {
                if (!parser.Run(tokenizer))
                {
                    Assert.That(parser.ErrorMessage, Is.Null);
                    Assert.Fail("AchievementScriptInterpreter.Run failed with no error message");
                }
            }
            else
            {
                Assert.That(parser.Run(tokenizer), Is.False);
                Assert.That(parser.ErrorMessage, Is.Not.Null);
            }

            return parser;
        }

        private static string GetRequirements(Achievement achievement)
        {
            var builder = new AchievementBuilder(achievement);
            return builder.RequirementsDebugString;
        }

        [Test]
        public void TestTitleAndGameId()
        {
            var parser = Parse("// Title\n// #ID=1234");
            Assert.That(parser.GameTitle, Is.EqualTo("Title"));
            Assert.That(parser.GameId, Is.EqualTo(1234));
        }

        [Test]
        public void TestAchievementFunction()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestVariables()
        {
            var parser = Parse("title = \"T\"\n" +
                               "desc = \"D\"\n" +
                               "points = 5\n" +
                               "trigger = byte(0x1234) == 1\n" +
                               "achievement(title, desc, points, trigger)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestNamedParameters()
        {
            var parser = Parse("title = \"T\"\n" +
                               "desc = \"D\"\n" +
                               "points = 5\n" +
                               "trigger = byte(0x1234) == 1\n" +
                               "achievement(points = points, trigger = trigger, title = title, description = desc)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestParameterizedFunction()
        {
            var parser = Parse("function trigger(i) => byte(0x1233 + i) == i\n" +
                               "achievement(\"T\", \"D\", 5, trigger(1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestAchievementNoTrigger()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5)", false);
            Assert.That(parser.ErrorMessage, Is.EqualTo("1:1 required parameter 'trigger' not provided"));
        }

        [Test]
        public void TestDictionaryLookup()
        {
            var parser = Parse("dict = { 1: \"T\", 2: \"D\" }\n" +
                               "achievement(dict[1], dict[2], 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestIf()
        {
            var parser = Parse("n = 1\n" +
                               "t = \"S\"\n" +
                               "if (n == 1) t = \"T\"\n" +
                               "achievement(t, \"D\", 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestElse()
        {
            var parser = Parse("n = 1\n" +
                               "if (n == 0) t = \"S\" else t = \"T\"\n" +
                               "achievement(t, \"D\", 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestElseBraces()
        {
            var parser = Parse("n = 1\n" +
                               "if (n == 0) { t = \"S\" } else { t = \"T\" }\n" +
                               "achievement(t, \"D\", 5, byte(0x1234) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestFor()
        {
            var parser = Parse("dict = { 1: \"T\", 2: \"T2\" }\n" +
                               "for k in dict {\n" +
                               "    achievement(dict[k], \"D\", 5, byte(0x1234) == 1)\n" +
                               "}");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(2));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));

            achievement = parser.Achievements.Last();
            Assert.That(achievement.Title, Is.EqualTo("T2"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestReturnFromFunction()
        {
            var parser = Parse("function f(i) {\n" +
                               "   if (i == 1)\n" +
                               "       return byte(0x1234) == 1\n" +
                               "   return byte(0x4567) == 1\n" +
                               "}\n" +
                               "achievement(\"T\", \"D\", 5, f(1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));

            var achievement = parser.Achievements.First();
            Assert.That(achievement.Title, Is.EqualTo("T"));
            Assert.That(achievement.Description, Is.EqualTo("D"));
            Assert.That(achievement.Points, Is.EqualTo(5));
            Assert.That(GetRequirements(achievement), Is.EqualTo("byte(0x001234) == 1"));
        }

        [Test]
        public void TestPrev()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, prev(byte(0x1234)) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("prev(byte(0x001234)) == 1"));
        }

        [Test]
        public void TestOnce()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x1234) == 1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("once(byte(0x001234) == 1)"));
        }

        [Test]
        public void TestRepeated()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, repeated(4, byte(0x1234) == 1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("repeated(4, byte(0x001234) == 1)"));
        }

        [Test]
        public void TestNever()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x4567) == 1) && never(byte(0x1234) == 1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("once(byte(0x004567) == 1) && never(byte(0x001234) == 1)"));
        }

        [Test]
        public void TestUnless()
        {
            var parser = Parse("achievement(\"T\", \"D\", 5, once(byte(0x4567) == 1) && unless(byte(0x1234) == 1))");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(GetRequirements(achievement), Is.EqualTo("once(byte(0x004567) == 1) && unless(byte(0x001234) == 1)"));
        }

        [Test]
        public void TestVariableScopeGlobal()
        {
            var parser = Parse("p = 5\n" +
                               "function test() { p = 6 }\n" +
                               "test()\n" +
                               "achievement(\"T\", \"D\", p, prev(byte(0x1234)) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(achievement.Points, Is.EqualTo(6));
        }

        [Test]
        public void TestVariableScopeParameter()
        {
            var parser = Parse("p = 5\n" +
                               "function test(p) { p = 6 }\n" +
                               "test(p)\n" +
                               "achievement(\"T\", \"D\", p, prev(byte(0x1234)) == 1)");
            Assert.That(parser.Achievements.Count(), Is.EqualTo(1));
            var achievement = parser.Achievements.First();
            Assert.That(achievement.Points, Is.EqualTo(5));
        }

        [Test]
        public void TestVariableScopeLocal()
        {
            var parser = Parse("function test() { p = 6 }\n" +
                               "test()\n" +
                               "achievement(\"T\", \"D\", p, prev(byte(0x1234)) == 1)", false);
            Assert.That(parser.ErrorMessage, Is.EqualTo("3:17 Unknown variable: p"));
        }

        [Test]
        public void TestRichPresenceDisplay()
        {
            var parser = Parse("rich_presence_display(\"simple string\")");
            Assert.That(parser.RichPresence, Is.EqualTo("Display:\r\nsimple string\r\n"));
        }

        [Test]
        public void TestRichPresenceValue()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234)))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValuePlusOne()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) + 1))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234_v1) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueMinusOne()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) - 1))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234_v-1) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueMultiply()
        {
            var parser = Parse("rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", byte(0x1234) * 10 + 1))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234*10_v1) here\r\n"));
        }

        [Test]
        public void TestRichPresenceValueFunction()
        {
            var parser = Parse("function test() => byte(0x1234)\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_value(\"Test\", test()))");
            Assert.That(parser.RichPresence, Is.EqualTo("Format:Test\r\nFormatType=VALUE\r\n\r\nDisplay:\r\nvalue @Test(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceLookup()
        {
            var parser = Parse("dict = { 1:\"Yes\", 2:\"No\" }\n" +
                               "rich_presence_display(\"value {0} here\", rich_presence_lookup(\"Test\", byte(0x1234), dict))");
            Assert.That(parser.RichPresence, Is.EqualTo("Lookup:Test\r\n1=Yes\r\n2=No\r\n\r\nDisplay:\r\nvalue @Test(0xH001234) here\r\n"));
        }

        [Test]
        public void TestRichPresenceInvalidIndex()
        {
            var parser = Parse("rich_presence_display(\"value {1} here\", rich_presence_value(\"Test\", byte(0x1234)))", false);
            Assert.That(parser.ErrorMessage, Is.EqualTo("1:23 invalid parameter index: 1"));
        }

        [Test]
        public void TestLeaderboard()
        {
            var parser = Parse("leaderboard(\"T\", \"D\", byte(0x1234) == 1, byte(0x1234) == 2, byte(0x1234) == 3, byte(0x4567))");
            Assert.That(parser.Leaderboards.Count(), Is.EqualTo(1));
            var leaderboard = parser.Leaderboards.First();
            Assert.That(leaderboard.Title, Is.EqualTo("T"));
            Assert.That(leaderboard.Description, Is.EqualTo("D"));
            Assert.That(leaderboard.Start, Is.EqualTo("0xH001234=1"));
            Assert.That(leaderboard.Cancel, Is.EqualTo("0xH001234=2"));
            Assert.That(leaderboard.Submit, Is.EqualTo("0xH001234=3"));
            Assert.That(leaderboard.Value, Is.EqualTo("0xH004567"));
        }
    }
}
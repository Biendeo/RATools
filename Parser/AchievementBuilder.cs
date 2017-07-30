﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RATools.Data;
using Jamiras.Components;
using System.Text;
using System.Collections;

namespace RATools.Parser.Internal
{
    [DebuggerDisplay("{Title} Core:{_core.Count} Alts:{_alts.Count}")]
    public class AchievementBuilder
    {
        public AchievementBuilder()
        {
            Current = _core = new List<Requirement>();
            _alts = new List<List<Requirement>>();
        }

        public AchievementBuilder(Achievement source)
            : this()
        {
            Title = source.Title;
            Description = source.Description;
            Points = source.Points;
            Id = source.Id;
            BadgeName = source.BadgeName;

            _core.AddRange(source.CoreRequirements);
            foreach (var alt in source.AlternateRequirements)
                _alts.Add(new List<Requirement>(alt));

            if (_alts.Count > 0)
                Current = _alts.Last();
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public int Points { get; set; }
        public int Id { get; set; }
        public string BadgeName { get; set; }

        private List<Requirement> _core;
        private List<List<Requirement>> _alts;

        internal List<Requirement> Current { get; set; }

        internal bool IsInNot { get; set; }
        internal int EqualityModifier { get; set; }

        public void BeginAlt()
        {
            if (Current == _core || Current.Count > 0)
            {
                var newAlt = new List<Requirement>();
                Current = newAlt;
                _alts.Add(newAlt);
            }
        }

        public Requirement LastRequirement
        {
            get { return Current.Last(); }
        }

        public Achievement ToAchievement()
        {
            var achievement = new Achievement { Title = Title, Description = Description, Points = Points, CoreRequirements = _core.ToArray(), Id = Id, BadgeName = BadgeName };
            var alts = new Requirement[_alts.Count][];
            for (int i = 0; i < _alts.Count; i++)
                alts[i] = _alts[i].ToArray();
            achievement.AlternateRequirements = alts;
            return achievement;
        }

        public void ParseRequirements(Tokenizer tokenizer)
        {
            do
            {
                var requirement = new Requirement();

                if (tokenizer.Match("R:"))
                    requirement.Type = RequirementType.ResetIf;
                else if (tokenizer.Match("P:"))
                    requirement.Type = RequirementType.PauseIf;

                requirement.Left = ReadField(tokenizer);
                requirement.Operator = ReadOperator(tokenizer);
                requirement.Right = ReadField(tokenizer);

                if (requirement.Right.Size == FieldSize.None)
                    requirement.Right = new Field { Type = requirement.Right.Type, Size = requirement.Left.Size, Value = requirement.Right.Value };

                Current.Add(requirement);

                if (tokenizer.NextChar == '.')
                {
                    tokenizer.Advance(); // first period
                    requirement.HitCount = (ushort)ReadNumber(tokenizer);
                    tokenizer.Advance(); // second period
                }

                switch (tokenizer.NextChar)
                {
                    default: 
                        return;

                    case '_': // &&
                        tokenizer.Advance();
                        continue;

                    case 'S': // ||
                        tokenizer.Advance();
                        BeginAlt();
                        continue;
                }

            } while (true);
        }

        private static uint ReadNumber(Tokenizer tokenizer)
        {
            uint value = 0;
            while (tokenizer.NextChar >= '0' && tokenizer.NextChar <= '9')
            {
                value *= 10;
                value += (uint)(tokenizer.NextChar - '0');
                tokenizer.Advance();
            }

            return value;
        }

        private static Field ReadField(Tokenizer tokenizer)
        {
            var fieldType = FieldType.MemoryAddress;
            if (tokenizer.NextChar == 'd')
            {
                fieldType = FieldType.PreviousValue;
                tokenizer.Advance();
            }

            if (!tokenizer.Match("0x"))
                return new Field { Type = FieldType.Value, Value = ReadNumber(tokenizer) };

            FieldSize size = FieldSize.None;
            switch (tokenizer.NextChar)
            {
                case 'M': size = FieldSize.Bit0; tokenizer.Advance(); break;
                case 'N': size = FieldSize.Bit1; tokenizer.Advance(); break;
                case 'O': size = FieldSize.Bit2; tokenizer.Advance(); break;
                case 'P': size = FieldSize.Bit3; tokenizer.Advance(); break;
                case 'Q': size = FieldSize.Bit4; tokenizer.Advance(); break;
                case 'R': size = FieldSize.Bit5; tokenizer.Advance(); break;
                case 'S': size = FieldSize.Bit6; tokenizer.Advance(); break;
                case 'T': size = FieldSize.Bit7; tokenizer.Advance(); break;
                case 'L': size = FieldSize.LowNibble; tokenizer.Advance(); break;
                case 'U': size = FieldSize.HighNibble; tokenizer.Advance(); break;
                case 'H': size = FieldSize.Byte; tokenizer.Advance(); break;
                case 'X': size = FieldSize.DWord; tokenizer.Advance(); break;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9': size = FieldSize.Word; break;
                case ' ': size = FieldSize.Word; tokenizer.Advance(); break;
            }

            uint address = 0;
            do
            {
                uint charValue = 255;
                switch (tokenizer.NextChar)
                {
                    case '0': charValue = 0; break;
                    case '1': charValue = 1; break;
                    case '2': charValue = 2; break;
                    case '3': charValue = 3; break;
                    case '4': charValue = 4; break;
                    case '5': charValue = 5; break;
                    case '6': charValue = 6; break;
                    case '7': charValue = 7; break;
                    case '8': charValue = 8; break;
                    case '9': charValue = 9; break;
                    case 'a':
                    case 'A': charValue = 10; break;
                    case 'b':
                    case 'B': charValue = 11; break;
                    case 'c':
                    case 'C': charValue = 12; break;
                    case 'd':
                    case 'D': charValue = 13; break;
                    case 'e':
                    case 'E': charValue = 14; break;
                    case 'f':
                    case 'F': charValue = 15; break;
                }

                if (charValue == 255)
                    break;

                tokenizer.Advance();
                address <<= 4;
                address += charValue;
            } while (true);

            return new Field { Size = size, Type = fieldType, Value = address };
        }

        private static RequirementOperator ReadOperator(Tokenizer tokenizer)
        {
            switch (tokenizer.NextChar)
            {
                case '=':
                    tokenizer.Advance();
                    return RequirementOperator.Equal;

                case '!':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        return RequirementOperator.NotEqual;
                    }
                    break;

                case '<':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        return RequirementOperator.LessThanOrEqual;
                    }
                    return RequirementOperator.LessThan;

                case '>':
                    tokenizer.Advance();
                    if (tokenizer.NextChar == '=')
                    {
                        tokenizer.Advance();
                        return RequirementOperator.GreaterThanOrEqual;
                    }
                    return RequirementOperator.GreaterThan;
            }

            return RequirementOperator.None;
        }

        public string SerializeRequirements()
        {
            return SerializeRequirements(_core, _alts);
        }

        public static string SerializeRequirements(Achievement achievement)
        {
            return SerializeRequirements(achievement.CoreRequirements, achievement.AlternateRequirements);
        }

        private static string SerializeRequirements(IEnumerable core, IEnumerable alts)
        {
            var builder = new StringBuilder();

            foreach (Requirement requirement in core)
            {
                SerializeRequirement(requirement, builder);
                builder.Append('_');
            }

            builder.Length--; // remove last _

            foreach (IEnumerable<Requirement> alt in alts)
            {
                builder.Append('S');
                foreach (Requirement requirement in alt)
                {
                    SerializeRequirement(requirement, builder);
                    builder.Append('_');
                }

                builder.Length--; // remove last _
            }

            return builder.ToString();
        }

        private static void SerializeRequirement(Requirement requirement, StringBuilder builder)
        {
            if (requirement.Type == RequirementType.ResetIf)
                builder.Append("R:");
            else if (requirement.Type == RequirementType.PauseIf)
                builder.Append("P:");

            SerializeField(requirement.Left, builder);

            switch (requirement.Operator)
            {
                case RequirementOperator.Equal: builder.Append('='); break;
                case RequirementOperator.NotEqual: builder.Append("!="); break;
                case RequirementOperator.LessThan: builder.Append('<'); break;
                case RequirementOperator.LessThanOrEqual: builder.Append("<="); break;
                case RequirementOperator.GreaterThan: builder.Append('>'); break;
                case RequirementOperator.GreaterThanOrEqual: builder.Append(">="); break;
            }

            SerializeField(requirement.Right, builder);

            if (requirement.HitCount > 0)
            {
                builder.Append('.');
                builder.Append(requirement.HitCount);
                builder.Append('.');
            }
        }

        private static void SerializeField(Field field, StringBuilder builder)
        {
            if (field.Type == FieldType.Value)
            {
                builder.Append(field.Value);
                return;
            }

            if (field.Type == FieldType.PreviousValue)
                builder.Append('d');

            builder.Append("0x");

            switch (field.Size)
            {
                case FieldSize.Bit0: builder.Append('M'); break;
                case FieldSize.Bit1: builder.Append('N'); break;
                case FieldSize.Bit2: builder.Append('O'); break;
                case FieldSize.Bit3: builder.Append('P'); break;
                case FieldSize.Bit4: builder.Append('Q'); break;
                case FieldSize.Bit5: builder.Append('R'); break;
                case FieldSize.Bit6: builder.Append('S'); break;
                case FieldSize.Bit7: builder.Append('T'); break;
                case FieldSize.LowNibble: builder.Append('L'); break;
                case FieldSize.HighNibble: builder.Append('U'); break;
                case FieldSize.Byte: builder.Append('H'); break;
                case FieldSize.Word: builder.Append(' ');  break;
                case FieldSize.DWord: builder.Append('X'); break;
            }

            builder.AppendFormat("{0:x6}", field.Value);
        }

        internal bool AreRequirementsSame(AchievementBuilder right)
        {
            if (!AreRequirementsSame(_core, right._core))
                return false;

            var enum1 = _alts.GetEnumerator();
            var enum2 = right._alts.GetEnumerator();
            while (enum1.MoveNext())
            {
                if (!enum2.MoveNext())
                    return false;

                if (!AreRequirementsSame(enum1.Current, enum2.Current))
                    return false;
            }

            return !enum2.MoveNext();
        }

        private static bool AreRequirementsSame(IEnumerable<Requirement> left, IEnumerable<Requirement> right)
        {
            var rightRequirements = new List<Requirement>(right);
            var enumerator = left.GetEnumerator();
            while (enumerator.MoveNext())
            {
                int index = -1;
                for (int i = 0; i < rightRequirements.Count; i++)
                {
                    if (rightRequirements[i] == enumerator.Current)
                    {
                        index = i;
                        break;
                    }
                }
                if (index == -1)
                    return false;

                rightRequirements.RemoveAt(index);
                if (rightRequirements.Count == 0)
                    return !enumerator.MoveNext();
            }

            return rightRequirements.Count == 0;
        }

        private static void NormalizeComparisons(List<Requirement> requirements)
        {
            var alwaysTrue = new List<Requirement>();

            foreach (var requirement in requirements)
            {
                switch (requirement.Left.Size)
                {
                    case FieldSize.Bit0:
                    case FieldSize.Bit1:
                    case FieldSize.Bit2:
                    case FieldSize.Bit3:
                    case FieldSize.Bit4:
                    case FieldSize.Bit5:
                    case FieldSize.Bit6:
                    case FieldSize.Bit7:
                        if (requirement.Right.Type == FieldType.Value)
                        {
                            if (requirement.Right.Value == 0)
                            {
                                switch (requirement.Operator)
                                {
                                    case RequirementOperator.NotEqual: // bit != 0 -> bit = 1
                                    case RequirementOperator.GreaterThan: // bit > 0 -> bit = 1
                                        requirement.Operator = RequirementOperator.Equal;
                                        requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 1 };
                                        break;

                                    case RequirementOperator.GreaterThanOrEqual: // bit >= 0 -> always true
                                        alwaysTrue.Add(requirement);
                                        break;
                                }
                            }
                            else // value is non-zero
                            {
                                if (requirement.Right.Value != 1)
                                    requirement.Right = new Field { Size = requirement.Left.Size, Type = FieldType.Value, Value = 1 };

                                switch (requirement.Operator)
                                {
                                    case RequirementOperator.NotEqual: // bit != 1 -> bit = 0
                                    case RequirementOperator.LessThan: // bit < 1 -> bit = 0
                                        requirement.Operator = RequirementOperator.Equal;
                                        requirement.Right = new Field { Size = requirement.Right.Size, Type = FieldType.Value, Value = 0 };
                                        break;

                                    case RequirementOperator.LessThanOrEqual: // bit <= 1 -> always true
                                        alwaysTrue.Add(requirement);
                                        break;
                                }
                            }
                        }

                        break;
                }
            }

            foreach (var requirement in alwaysTrue)
                requirements.Remove(requirement);
        }

        private void PromoteCommonAltsToCore()
        {
            var requirementsFoundInAll = new List<Requirement>();
            foreach (var requirement in _alts[0])
            {
                bool foundInAll = true;
                for (int i = 1; i < _alts.Count; i++)
                {
                    if (!_alts[i].Any(a => a == requirement))
                    {
                        foundInAll = false;
                        break;
                    }
                }

                if (foundInAll)
                    requirementsFoundInAll.Add(requirement);
            }

            foreach (var requirement in requirementsFoundInAll)
            {
                foreach (var alt in _alts)
                {
                    for (int i = alt.Count - 1; i >= 0; i--)
                    {
                        if (alt[i] == requirement)
                            alt.RemoveAt(i);
                    }
                }

                _core.Add(requirement);
            }
        }

        private void RemoveDuplicates(List<Requirement> requirements)
        {
            for (int i = 0; i < requirements.Count; i++)
            {
                for (int j = requirements.Count - 1; j > i; j--)
                {
                    if (requirements[j] == requirements[i])
                        requirements.RemoveAt(j);
                }
            }
        }

        private void RemoveAltsAlreadyInCore(List<Requirement> requirements)
        {
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                if (_core.Any(r => r == requirements[i]))
                {
                    requirements.RemoveAt(i);
                    continue;
                }
            }
        }

        private static void RemoveRedundancies(List<Requirement> requirements)
        {
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                var requirement = requirements[i];
                if (requirement.Right.Type != FieldType.Value)
                    continue;

                bool redundant = false;
                for (int j = 0; j < requirements.Count; j++)
                {
                    if (j == i)
                        continue;

                    var check = requirements[j];
                    if (check.Right.Type != FieldType.Value)
                        continue;
                    if (check.Left.Value != requirement.Left.Value)
                        continue;

                    switch (check.Operator)
                    {
                        case RequirementOperator.Equal:
                            if (requirement.Operator == RequirementOperator.GreaterThan && requirement.Right.Value < check.Right.Value)
                                redundant = true;
                            else if (requirement.Operator == RequirementOperator.GreaterThanOrEqual && requirement.Right.Value <= check.Right.Value)
                                redundant = true;
                            else if (requirement.Operator == RequirementOperator.LessThan && requirement.Right.Value > check.Right.Value)
                                redundant = true;
                            else if (requirement.Operator == RequirementOperator.LessThanOrEqual && requirement.Right.Value >= check.Right.Value)
                                redundant = true;
                            break;

                        case RequirementOperator.GreaterThan:
                            if (requirement.Operator == RequirementOperator.GreaterThan && requirement.Right.Value < check.Right.Value)
                                redundant = true;
                            else if (requirement.Operator == RequirementOperator.GreaterThanOrEqual && requirement.Right.Value <= check.Right.Value)
                                redundant = true;
                            break;

                        case RequirementOperator.GreaterThanOrEqual:
                            if (requirement.Operator == RequirementOperator.GreaterThan && requirement.Right.Value < check.Right.Value)
                                redundant = true;
                            else if (requirement.Operator == RequirementOperator.GreaterThanOrEqual && requirement.Right.Value < check.Right.Value)
                                redundant = true;
                            break;

                        case RequirementOperator.LessThan:
                            if (requirement.Operator == RequirementOperator.LessThan && requirement.Right.Value > check.Right.Value)
                                redundant = true;
                            else if (requirement.Operator == RequirementOperator.LessThanOrEqual && requirement.Right.Value >= check.Right.Value)
                                redundant = true;
                            break;

                        case RequirementOperator.LessThanOrEqual:
                            if (requirement.Operator == RequirementOperator.LessThan && requirement.Right.Value > check.Right.Value)
                                redundant = true;
                            else if (requirement.Operator == RequirementOperator.LessThanOrEqual && requirement.Right.Value > check.Right.Value)
                                redundant = true;
                            break;
                    }

                    if (redundant)
                        break;
                }

                if (redundant)
                {
                    requirements.RemoveAt(i);
                    continue;
                }
            }
        }

        private static bool IsMergable(Requirement requirement)
        {
            if (requirement.Operator != RequirementOperator.Equal)
                return false;
            if (requirement.Right.Type != FieldType.Value)
                return false;
            if (requirement.HitCount != 0)
                return false;
            if (requirement.Type != RequirementType.None)
                return false;

            return true;
        }

        private void MergeBits(List<Requirement> requirements)
        {
            var references = new TinyDictionary<uint, int>();
            foreach (var requirement in requirements)
            {
                if (!IsMergable(requirement))
                    continue;

                int flags;
                references.TryGetValue(requirement.Left.Value, out flags);
                switch (requirement.Left.Size)
                {
                    case FieldSize.Bit0:
                        flags |= 0x01;
                        if (requirement.Right.Value != 0)
                            flags |= 0x0100;
                        break;
                    case FieldSize.Bit1:
                        flags |= 0x02;
                        if (requirement.Right.Value != 0)
                            flags |= 0x0200;
                        break;
                    case FieldSize.Bit2:
                        flags |= 0x04;
                        if (requirement.Right.Value != 0)
                            flags |= 0x0400;
                        break;
                    case FieldSize.Bit3:
                        flags |= 0x08;
                        if (requirement.Right.Value != 0)
                            flags |= 0x0800;
                        break;
                    case FieldSize.Bit4:
                        flags |= 0x10;
                        if (requirement.Right.Value != 0)
                            flags |= 0x1000;
                        break;
                    case FieldSize.Bit5:
                        flags |= 0x20;
                        if (requirement.Right.Value != 0)
                            flags |= 0x2000;
                        break;
                    case FieldSize.Bit6:
                        flags |= 0x40;
                        if (requirement.Right.Value != 0)
                            flags |= 0x4000;
                        break;
                    case FieldSize.Bit7:
                        flags |= 0x80;
                        if (requirement.Right.Value != 0)
                            flags |= 0x8000;
                        break;
                    case FieldSize.LowNibble:
                        flags |= 0x0F;
                        flags |= (ushort)(requirement.Right.Value & 0x0F) << 8;
                        break;
                    case FieldSize.HighNibble:
                        flags |= 0xF0;
                        flags |= (ushort)(requirement.Right.Value & 0x0F) << 12;
                        break;
                    case FieldSize.Byte:
                        flags |= 0xFF;
                        flags |= (ushort)(requirement.Right.Value & 0xFF) << 8;
                        break;
                }

                references[requirement.Left.Value] = flags;
            }

            foreach (var kvp in references)
            {
                if ((kvp.Value & 0xFF) == 0xFF)
                {
                    MergeBits(requirements, kvp.Key, FieldSize.Byte, (kvp.Value >> 8) & 0xFF);
                }
                else
                {
                    if ((kvp.Value & 0x0F) == 0x0F)
                        MergeBits(requirements, kvp.Key, FieldSize.LowNibble, (kvp.Value >> 8) & 0x0F);
                    if ((kvp.Value & 0xF0) == 0xF0)
                        MergeBits(requirements, kvp.Key, FieldSize.HighNibble, (kvp.Value >> 12) & 0x0F);
                }
            }
        }

        private static void MergeBits(List<Requirement> requirements, uint address, FieldSize newSize, int newValue)
        {
            bool insert = true;
            int insertAt = 0;
            for (int i = requirements.Count - 1; i >= 0; i--)
            {
                if (!IsMergable(requirements[i]))
                    continue;

                var requirement = requirements[i];
                if (requirement.Left.Value != address)
                    continue;

                if (requirement.Left.Size == newSize)
                {
                    if (requirement.Right.Value != newValue)
                        requirement.Right = new Field { Size = newSize, Type = FieldType.Value, Value = (uint)newValue };

                    insert = false;
                    continue;
                }

                bool delete = false;
                switch (newSize)
                {
                    case FieldSize.Byte:
                        switch (requirement.Left.Size)
                        {
                            case FieldSize.Bit0:
                            case FieldSize.Bit1:
                            case FieldSize.Bit2:
                            case FieldSize.Bit3:
                            case FieldSize.Bit4:
                            case FieldSize.Bit5:
                            case FieldSize.Bit6:
                            case FieldSize.Bit7:
                            case FieldSize.LowNibble:
                            case FieldSize.HighNibble:
                                delete = true;
                                break;
                        }
                        break;

                    case FieldSize.LowNibble:
                        switch (requirement.Left.Size)
                        {
                            case FieldSize.Bit0:
                            case FieldSize.Bit1:
                            case FieldSize.Bit2:
                            case FieldSize.Bit3:
                                delete = true;
                                break;
                        }
                        break;

                    case FieldSize.HighNibble:
                        switch (requirement.Left.Size)
                        {
                            case FieldSize.Bit4:
                            case FieldSize.Bit5:
                            case FieldSize.Bit6:
                            case FieldSize.Bit7:
                                delete = true;
                                break;
                        }
                        break;
                }

                if (delete)
                {
                    requirements.RemoveAt(i);
                    insertAt = i;
                    continue;
                }
            }

            if (insert)
            {
                var requirement = new Requirement();
                requirement.Left = new Field { Size = newSize, Type = FieldType.MemoryAddress, Value = address };
                requirement.Operator = RequirementOperator.Equal;
                requirement.Right = new Field { Size = newSize, Type = FieldType.Value, Value = (uint)newValue };
                requirements.Insert(insertAt, requirement);
            }
        }

        public void Optimize()
        {
            // normalize BitX() methods to compare against 1
            NormalizeComparisons(_core);
            foreach (var alt in _alts)
                NormalizeComparisons(alt);

            // remove duplicates
            RemoveDuplicates(_core);
            foreach (var alt in _alts)
            {
                RemoveDuplicates(alt);
                RemoveAltsAlreadyInCore(alt);
            }

            // remove redundancies (i > 3 && i > 5) => (i > 5)
            RemoveRedundancies(_core);
            foreach (var alt in _alts)
                RemoveRedundancies(alt);

            // bit1(x) && bit2(x) && bit3(x) && bit4(x) => low4(x)
            MergeBits(_core);
            foreach (var alt in _alts)
                MergeBits(alt);

            // identify any item common to all alts and promote it to core
            if (_alts.Count > 1)
                PromoteCommonAltsToCore();
        }
    }
}

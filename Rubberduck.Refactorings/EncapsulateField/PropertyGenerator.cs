﻿using Rubberduck.Parsing.Grammar;
using Rubberduck.SmartIndenter;
using System;
using System.Collections.Generic;

namespace Rubberduck.Refactorings.EncapsulateField
{
    public interface IPropertyGeneratorAttributes
    {
        string PropertyName { get; }
        string BackingField { get; }
        string AsTypeName { get; }
        string ParameterName { get; }
        bool GenerateLetter { get; }
        bool GenerateSetter { get; }
        bool UsesSetAssignment { get; }
        bool IsUDTProperty { get; }
    }

    public class PropertyAttributeSet : IPropertyGeneratorAttributes
    {
        public string PropertyName { get; set; }
        public string BackingField { get; set; }
        public string AsTypeName { get; set; }
        public string ParameterName { get; set; }
        public bool GenerateLetter { get; set; }
        public bool GenerateSetter { get; set; }
        public bool UsesSetAssignment { get; set; }
        public bool IsUDTProperty { get; set; }
    }

    public class PropertyGenerator
    {
        public PropertyGenerator() { }

        public string PropertyName { get; set; }
        public string BackingField { get; set; }
        public string AsTypeName { get; set; }
        public string ParameterName { get; set; }
        public bool GenerateLetter { get; set; }
        public bool GenerateSetter { get; set; }
        public bool UsesSetAssignment { get; set; }
        public bool IsUDTProperty { get; set; }

        public string AsPropertyBlock(IPropertyGeneratorAttributes spec, IIndenter indenter)
        {
            PropertyName = spec.PropertyName;
            BackingField = spec.BackingField;
            AsTypeName = spec.AsTypeName;
            ParameterName = spec.ParameterName;
            GenerateLetter = spec.GenerateLetter;
            GenerateSetter = spec.GenerateSetter;
            UsesSetAssignment = spec.UsesSetAssignment;
            IsUDTProperty = spec.IsUDTProperty;
            return string.Join(Environment.NewLine, indenter.Indent(AsLines, true));
        }

        private string AllPropertyCode =>
            $"{GetterCode}{(GenerateLetter ? LetterCode : string.Empty)}{(GenerateSetter ? SetterCode : string.Empty)}";

        private IEnumerable<string> AsLines => AllPropertyCode.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        private string GetterCode
        {
            get
            {
                if (GenerateSetter && GenerateLetter)
                {
                    return string.Join(Environment.NewLine,
                                       $"Public Property Get {PropertyName}() As {AsTypeName}",
                                       $"    If IsObject({BackingField}) Then",
                                       $"        Set {PropertyName} = {BackingField}",
                                       "    Else",
                                       $"        {PropertyName} = {BackingField}",
                                       "    End If",
                                       "End Property",
                                       Environment.NewLine);
                }

                return string.Join(Environment.NewLine,
                                   $"Public Property Get {PropertyName}() As {AsTypeName}",
                                   $"    {(UsesSetAssignment ? "Set " : string.Empty)}{PropertyName} = {BackingField}",
                                   "End Property",
                                   Environment.NewLine);
            }
        }

        private string SetterCode
        {
            get
            {
                if (!GenerateSetter)
                {
                    return string.Empty;
                }
                return string.Join(Environment.NewLine,
                                   $"Public Property Set {PropertyName}(ByVal {ParameterName} As {AsTypeName})",
                                   $"    Set {BackingField} = {ParameterName}",
                                   "End Property",
                                   Environment.NewLine);
            }
        }

        private string LetterCode
        {
            get
            {
                if (!GenerateLetter)
                {
                    return string.Empty;
                }

                var byVal_byRef = IsUDTProperty ? Tokens.ByRef : Tokens.ByVal;

                return string.Join(Environment.NewLine,
                                   $"Public Property Let {PropertyName}({byVal_byRef} {ParameterName} As {AsTypeName})",
                                   $"    {BackingField} = {ParameterName}",
                                   "End Property",
                                   Environment.NewLine);
            }
        }
    }
}

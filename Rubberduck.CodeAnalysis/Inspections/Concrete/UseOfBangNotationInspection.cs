﻿using Rubberduck.CodeAnalysis.Inspections;
using Rubberduck.Inspections.Abstract;
using Rubberduck.Resources.Inspections;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.VBA.DeclarationCaching;

namespace Rubberduck.Inspections.Concrete
{
    /// <summary>
    /// Identifies the use of bang notation, formally known as dictionary access expression.
    /// </summary>
    /// <why>
    /// A dictionary access expression looks like a strongly typed call, but it actually is a stringly typed access to the parameterized default member of the object. 
    /// </why>
    /// <example hasresult="true">
    /// <![CDATA[
    /// Public Sub DoSomething(ByVal wkb As Excel.Workbook)
    ///     wkb.Worksheets!MySheet.Range("A1").Value = 42
    /// End Sub
    /// ]]>
    /// </example>
    /// <example hasresult="true">
    /// <![CDATA[
    /// Public Sub DoSomething(ByVal wkb As Excel.Workbook)
    ///     With wkb.Worksheets
    ///         !MySheet.Range("A1").Value = 42
    ///     End With
    /// End Sub
    /// ]]>
    /// </example>
    /// <example hasresult="false">
    /// <![CDATA[
    /// Public Sub DoSomething(ByVal wkb As Excel.Workbook)
    ///     wkb.Worksheets("MySheet").Range("A1").Value = 42
    /// End Sub
    /// ]]>
    /// </example>
    /// <example hasresult="false">
    /// <![CDATA[
    /// Public Sub DoSomething(ByVal wkb As Excel.Workbook)
    ///     wkb.Worksheets.Item("MySheet").Range("A1").Value = 42
    /// End Sub
    /// ]]>
    /// </example>
    /// <example hasresult="false">
    /// <![CDATA[
    /// Public Sub DoSomething(ByVal wkb As Excel.Workbook)
    ///     With wkb.Worksheets
    ///         .Item("MySheet").Range("A1").Value = 42
    ///     End With
    /// End Sub
    /// ]]>
    /// </example>
    internal sealed class UseOfBangNotationInspection : IdentifierReferenceInspectionBase
    {
        public UseOfBangNotationInspection(IDeclarationFinderProvider declarationFinderProvider)
            : base(declarationFinderProvider)
        {
            Severity = CodeInspectionSeverity.Suggestion;
        }

        protected override bool IsResultReference(IdentifierReference reference, DeclarationFinder finder)
        {
            return reference.IsIndexedDefaultMemberAccess
                   && reference.DefaultMemberRecursionDepth == 1
                   && reference.Context is VBAParser.DictionaryAccessContext;
        }

        protected override string ResultDescription(IdentifierReference reference)
        {
            var expression = reference.IdentifierName;
            return string.Format(InspectionResults.UseOfBangNotationInspection, expression);
        }
    }
}
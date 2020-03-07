using System.Collections.Generic;
using System.Linq;
using Rubberduck.CodeAnalysis.Inspections;
using Rubberduck.CodeAnalysis.Inspections.Attributes;
using Rubberduck.CodeAnalysis.QuickFixes.Abstract;
using Rubberduck.Parsing;
using Rubberduck.Parsing.Annotations;
using Rubberduck.Parsing.Rewriter;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;

namespace Rubberduck.Inspections.QuickFixes
{
    /// <summary>
    /// Adds an '@Ignore annotation to ignore a specific inspection result. Applicable to all inspections whose results can be annotated in a module.
    /// </summary>
    /// <canfix procedure="false" module="false" project="false" />
    /// <example>
    /// <before>
    /// <![CDATA[
    /// Public Sub DoSomething()
    ///     Dim value As Long
    ///     value = 42
    ///     Debug.Print 42
    /// End Sub
    /// ]]>
    /// </before>
    /// <after>
    /// <![CDATA[
    /// Public Sub DoSomething()
    ///     '@Ignore VariableNotUsed
    ///     Dim value As Long
    ///     value = 42
    ///     Debug.Print 42
    /// End Sub
    /// ]]>
    /// </after>
    /// </example>
    internal sealed class IgnoreOnceQuickFix : QuickFixBase
    {
        private readonly RubberduckParserState _state;
        private readonly IAnnotationUpdater _annotationUpdater;

        public IgnoreOnceQuickFix(IAnnotationUpdater annotationUpdater, RubberduckParserState state, IEnumerable<IInspection> inspections)
            : base(inspections.Select(s => s.GetType()).Where(i => i.CustomAttributes.All(a => a.AttributeType != typeof(CannotAnnotateAttribute))).ToArray())
        {
            _state = state;
            _annotationUpdater = annotationUpdater;
        }

        public override bool CanFixInProcedure => false;
        public override bool CanFixInModule => false;
        public override bool CanFixInProject => false;

        public override void Fix(IInspectionResult result, IRewriteSession rewriteSession)
        {
            if (result.Target?.DeclarationType.HasFlag(DeclarationType.Module) ?? false)
            {
                FixModule(result, rewriteSession);
            }
            else
            {
                FixNonModule(result, rewriteSession);
            }
        }

        private void FixNonModule(IInspectionResult result, IRewriteSession rewriteSession)
        {
            var module = result.QualifiedSelection.QualifiedName;
            var lineToAnnotate = result.QualifiedSelection.Selection.StartLine;
            var existingIgnoreAnnotation = _state.DeclarationFinder
                .FindAnnotations(module, lineToAnnotate)
                .FirstOrDefault(pta => pta.Annotation is IgnoreAnnotation);

            var annotationInfo = new IgnoreAnnotation();
            if (existingIgnoreAnnotation != null)
            {
                var annotationValues = existingIgnoreAnnotation.AnnotationArguments.ToList();
                annotationValues.Insert(0, result.Inspection.AnnotationName);
                _annotationUpdater.UpdateAnnotation(rewriteSession, existingIgnoreAnnotation, annotationInfo, annotationValues);
            }
            else
            {
                var annotationValues = new List<string> { result.Inspection.AnnotationName };
                _annotationUpdater.AddAnnotation(rewriteSession, new QualifiedContext(module, result.Context), annotationInfo, annotationValues);
            }
        }

        private void FixModule(IInspectionResult result, IRewriteSession rewriteSession)
        {
            var moduleDeclaration = result.Target;
            var existingIgnoreModuleAnnotation = moduleDeclaration.Annotations
                .Where(pta => pta.Annotation is IgnoreModuleAnnotation)
                .FirstOrDefault();

            var annotationType = new IgnoreModuleAnnotation();
            if (existingIgnoreModuleAnnotation != null)
            {
                var annotationValues = existingIgnoreModuleAnnotation.AnnotationArguments.ToList();
                annotationValues.Insert(0, result.Inspection.AnnotationName);
                _annotationUpdater.UpdateAnnotation(rewriteSession, existingIgnoreModuleAnnotation, annotationType, annotationValues);
            }
            else
            {
                var annotationValues = new List<string> { result.Inspection.AnnotationName };
                _annotationUpdater.AddAnnotation(rewriteSession, moduleDeclaration, annotationType, annotationValues);
            }
        }

        public override string Description(IInspectionResult result) => Resources.Inspections.QuickFixes.IgnoreOnce;
    }
}

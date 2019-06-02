﻿using System.Collections.Generic;
using System.Linq;
using Rubberduck.Inspections.Abstract;
using Rubberduck.Inspections.Inspections.Extensions;
using Rubberduck.Inspections.Results;
using Rubberduck.Parsing;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Inspections.Abstract;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;
using Rubberduck.Parsing.VBA.DeclarationCaching;
using Rubberduck.Resources.Inspections;
using Rubberduck.VBEditor;

namespace Rubberduck.CodeAnalysis.Inspections.Concrete
{
    public class SetAssignmentWithIncompatibleObjectTypeInspection : InspectionBase
    {
        private readonly IDeclarationFinderProvider _declarationFinderProvider;

        private const string UndeterminedValue = "Undetermined";

        /// <summary>
        /// Locates assignments to object variables for which the RHS does not have a compatible declared type. 
        /// </summary>
        /// <why>
        /// The VBA compiler does not check whether different object types are compatible. Instead there is a runtime error whenever the types are incompatible.
        /// </why>
        /// <example>
        /// <![CDATA[
        /// IInterface:
        ///
        /// Public Sub DoSomething()
        /// End Sub
        ///
        /// ------------------------------
        /// Class1:
        ///
        ///'No Implements IInterface
        /// 
        /// Public Sub DoSomething()
        /// End Sub
        ///
        /// ------------------------------
        /// Module1:
        /// 
        /// Public Sub DoIt()
        ///     Dim cls As Class1
        ///     Dim intrfc As IInterface
        ///
        ///     Set cls = New Class1
        ///     Set intrfc = cls 
        /// End Sub
        /// ]]>
        /// </example>
        /// <example>
        /// <![CDATA[
        /// IInterface:
        ///
        /// Public Sub DoSomething()
        /// End Sub
        ///
        /// ------------------------------
        /// Class1:
        ///
        /// Implements IInterface
        /// 
        /// Private Sub IInterface_DoSomething()
        /// End Sub
        ///
        /// ------------------------------
        /// Module1:
        /// 
        /// Public Sub DoIt()
        ///     Dim cls As Class1
        ///     Dim intrfc As IInterface
        ///
        ///     Set cls = New Class1
        ///     Set intrfc = cls 
        /// End Sub
        /// ]]>
        /// </example>
        public SetAssignmentWithIncompatibleObjectTypeInspection(RubberduckParserState state)
            : base(state)
        {
            _declarationFinderProvider = state;
        }

        protected override IEnumerable<IInspectionResult> DoGetInspectionResults()
        {
            var finder = _declarationFinderProvider.DeclarationFinder;

            var offendingAssignments = StronglyTypedObjectVariables(finder)
                .SelectMany(SetAssignments)
                .Select(setAssignment => SetAssignmentWithAssignedTypeName(setAssignment, finder))
                .Where(setAssignmentWithAssignedTypeName => setAssignmentWithAssignedTypeName.assignedTypeName != UndeterminedValue
                    && !SetAssignmentPossiblyLegal(setAssignmentWithAssignedTypeName));

            return offendingAssignments
                .Where(setAssignmentWithAssignedTypeName => !IsIgnored(setAssignmentWithAssignedTypeName.setAssignment))
                .Select(setAssignmentWithAssignedTypeName => InspectionResult(setAssignmentWithAssignedTypeName, _declarationFinderProvider));
        }


        private IEnumerable<Declaration> StronglyTypedObjectVariables(DeclarationFinder declarationFinder)
        {
            return declarationFinder.DeclarationsWithType(DeclarationType.Variable)
                .Where(declaration => declaration.IsObject
                                      && declaration.AsTypeDeclaration != null);
        }

        private IEnumerable<IdentifierReference> SetAssignments(Declaration declaration)
        {
            return declaration.References.Where(reference => reference.IsSetAssignment);
        }

        private (IdentifierReference setAssignment, string assignedTypeName) SetAssignmentWithAssignedTypeName(IdentifierReference setAssignment, DeclarationFinder finder)
        {
            return (setAssignment, SetTypeNameOfExpression(RHS(setAssignment), setAssignment.QualifiedModuleName, finder));
        }

        private VBAParser.ExpressionContext RHS(IdentifierReference setAssignment)
        {
            return setAssignment.Context.GetAncestor<VBAParser.SetStmtContext>().expression();
        }

        private string SetTypeNameOfExpression(VBAParser.ExpressionContext expression, QualifiedModuleName containingModule, DeclarationFinder finder)
        {
            switch (expression)
            {
                case VBAParser.LExprContext lExpression:
                    return SetTypeNameOfExpression(lExpression.lExpression(), containingModule, finder);
                case VBAParser.NewExprContext newExpression:
                    return UndeterminedValue;
                default:
                    return UndeterminedValue;
            }
        }

        private string SetTypeNameOfExpression(VBAParser.LExpressionContext lExpression, QualifiedModuleName containingModule, DeclarationFinder finder)
        {
            switch (lExpression)
            {
                case VBAParser.SimpleNameExprContext simpleNameExpression:
                    return SetTypeNameOfExpression(simpleNameExpression.identifier(), containingModule, finder);
                case VBAParser.InstanceExprContext instanceExpression:
                    return SetTypeNameOfInstance(containingModule);
                default:
                    return UndeterminedValue;
            }
        }

        private string SetTypeNameOfExpression(VBAParser.IdentifierContext identifier, QualifiedModuleName containingModule, DeclarationFinder finder)
        {
            var typeName = finder.IdentifierReferences(identifier, containingModule)
                .Select(reference => reference.Declaration)
                .Where(declaration => declaration.IsObject)
                .Select(declaration => declaration.FullAsTypeName)
                .FirstOrDefault();
            return typeName ?? UndeterminedValue;
        }

        private string SetTypeNameOfInstance(QualifiedModuleName instance)
        {
            return instance.ToString();
        }
        
        private bool SetAssignmentPossiblyLegal((IdentifierReference setAssignment, string assignedTypeName) setAssignmentWithAssignedType)
        {
            var (setAssignment, assignedTypeName) = setAssignmentWithAssignedType;
            
            return SetAssignmentPossiblyLegal(setAssignment.Declaration, assignedTypeName);
        }

        private bool SetAssignmentPossiblyLegal(Declaration declaration, string assignedTypeName)
        {
            return assignedTypeName == declaration.FullAsTypeName
                || assignedTypeName == Tokens.Variant
                || assignedTypeName == Tokens.Object
                || HasBaseType(declaration, assignedTypeName)
                || HasSubType(declaration, assignedTypeName);
        }

        private bool HasBaseType(Declaration declaration, string typeName)
        {
            var ownType = declaration.AsTypeDeclaration;
            if (ownType == null || !(ownType is ClassModuleDeclaration classType))
            {
                return false;
            }

            return classType.Subtypes.Select(subtype => subtype.QualifiedModuleName.ToString()).Contains(typeName);
        }

        private bool HasSubType(Declaration declaration, string typeName)
        {
            var ownType = declaration.AsTypeDeclaration;
            if (ownType == null || !(ownType is ClassModuleDeclaration classType))
            {
                return false;
            }

            return classType.Supertypes.Select(supertype => supertype.QualifiedModuleName.ToString()).Contains(typeName);
        }

        private bool IsIgnored(IdentifierReference assignment)
        {
            return assignment.IsIgnoringInspectionResultFor(AnnotationName)
                   // Ignoring the Declaration disqualifies all assignments
                   || assignment.Declaration.IsIgnoringInspectionResultFor(AnnotationName);
        }

        private IInspectionResult InspectionResult((IdentifierReference setAssignment, string assignedTypeName) setAssignmentWithAssignedType, IDeclarationFinderProvider declarationFinderProvider)
        {
            var (setAssignment, assignedTypeName) = setAssignmentWithAssignedType;
            return new IdentifierReferenceInspectionResult(this,
                ResultDescription(setAssignment, assignedTypeName),
                declarationFinderProvider,
                setAssignment);
        }

        private string ResultDescription(IdentifierReference setAssignment, string assignedTypeName)
        {
            var declarationName = setAssignment.Declaration.IdentifierName;
            var variableTypeName = setAssignment.Declaration.FullAsTypeName;
            return string.Format(InspectionResults.SetAssignmentWithIncompatibleObjectTypeInspection, declarationName, variableTypeName, assignedTypeName);
        }
    }
}
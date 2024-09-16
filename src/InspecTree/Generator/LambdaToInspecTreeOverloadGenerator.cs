using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace InspecTree.Generator
{
  [Generator]
  public class LambdaToInspecTreeOverloadGenerator : ISourceGenerator
  {
    private readonly IOverloadToSourceConverter _overloadToSourceConverter;

    public LambdaToInspecTreeOverloadGenerator() : this(null) { }

    public LambdaToInspecTreeOverloadGenerator(IOverloadToSourceConverter overloadToSourceConverter = null)
    {
      _overloadToSourceConverter = overloadToSourceConverter ?? new OverloadToSourceConverter();
    }

    public void Initialize(GeneratorInitializationContext context) =>
      context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());

    public void Execute(GeneratorExecutionContext context)
    {
      // Retrieve the collected methods
      if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
        return;

      var methods = receiver.CandidateMethods;
      var generatedOverloads = new List<GeneratedOverload>();

      foreach (var method in methods)
      {
        var methodSymbol = context.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method);

        var usingsInOriginalFile = method.SyntaxTree.GetRoot()
          .DescendantNodes()
          .OfType<UsingDirectiveSyntax>()
          .Select(u => u.Name.ToString())
          .ToList();
        var usings = usingsInOriginalFile
          .Distinct()
          .Where(u => u != "InspecTree")
          .ToList();
        var namespaceName = methodSymbol.ContainingNamespace.ToDisplayString();
        var classDeclaration = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        var className = classDeclaration.Identifier.Text;
        var methodName = method.Identifier.Text;
        var returnType = method.ReturnType.ToString();

        generatedOverloads.Add(new GeneratedOverload(
          usings: usings,
          namespaceName: namespaceName,
          className: className,
          classAccessModifier: classDeclaration.Modifiers.ToFullString().Trim(),
          methodAccessModifier: method.Modifiers.ToFullString().Trim(),
          returnType: returnType,
          methodName: methodName,
          parameters: method.ParameterList.Parameters.Select(p => new GeneratedParameter(
            parameterType: p.Type.ToString()
              .StartsWith("InspecTree<")
              ? p.Type.ToString().Substring("InspecTree<".Length, p.Type.ToString().Length - "InspecTree<".Length - 1)
              : p.Type.ToString(),
            parameterName: p.Identifier.ToString()
          )).ToList()
        ));
      }

      var sourceFiles = _overloadToSourceConverter.ConvertToSource(generatedOverloads);

      foreach (var sourceFile in sourceFiles)
        context.AddSource(sourceFile.FileName, sourceFile.SourceText);
    }

    private sealed class SyntaxReceiver : ISyntaxReceiver
    {
      public List<MethodDeclarationSyntax> CandidateMethods { get; } = new List<MethodDeclarationSyntax>();

      public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
      {
        if (syntaxNode is MethodDeclarationSyntax methodDeclaration
           && methodDeclaration.ParameterList.Parameters.Count > 0
           && methodDeclaration.ParameterList.Parameters.Any(p => p.Type.ToString().Contains(nameof(InspecTree))))
        {
          CandidateMethods.Add(methodDeclaration);
          return;
        }
      }
    }
  }
}

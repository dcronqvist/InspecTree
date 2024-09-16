using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Text;

namespace InspecTree.Generator
{
  [Generator]
  public class InspecTreeOverloadInterceptorGenerator : IIncrementalGenerator
  {
    private readonly IInterceptedInvocationToSourceConverter _interceptedInvocationToSourceConverter;

    public InspecTreeOverloadInterceptorGenerator() : this(null) { }

    public InspecTreeOverloadInterceptorGenerator(IInterceptedInvocationToSourceConverter interceptedInvocationToSourceConverter = null)
    {
      _interceptedInvocationToSourceConverter = interceptedInvocationToSourceConverter ?? new InterceptedInvocationToSourceConverter();
    }

    private bool MethodHasInspecTreeParameter(MethodDeclarationSyntax methodDeclaration)
    {
      return methodDeclaration.ParameterList.Parameters
        .Any(p => p.Type.ToString().StartsWith("InspecTree<", StringComparison.InvariantCulture));
    }

    private IncrementalValuesProvider<MethodDeclarationSyntax> GetProviderForMethodDeclarationsToIntercept(IncrementalGeneratorInitializationContext context)
    {
      return context.SyntaxProvider.CreateSyntaxProvider(
        predicate: (node, _) =>
        {
          if (!(node is MethodDeclarationSyntax methodDeclaration))
            return false;

          return MethodHasInspecTreeParameter(methodDeclaration);
        },
        transform: (syntaxContext, token) => syntaxContext.Node as MethodDeclarationSyntax);
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
      var methodDeclarationsToInterceptFrom = GetProviderForMethodDeclarationsToIntercept(context).Collect();

      var invocationProvider = context.SyntaxProvider.CreateSyntaxProvider(
        predicate: (node, _) => node is InvocationExpressionSyntax,
        transform: (syntaxContext, token) =>
        {
          var invocationExpression = syntaxContext.Node as InvocationExpressionSyntax;
          var invokedMethodSymbolInfo = syntaxContext.SemanticModel.GetSymbolInfo(invocationExpression.Expression, token);

          if (!(invokedMethodSymbolInfo.CandidateSymbols.FirstOrDefault() is IMethodSymbol calledMethod))
            return null;

          var declarationOfCalledMethod = syntaxContext.SemanticModel.Compilation.SyntaxTrees
            .SelectMany(st => st.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Single(mds => mds.Identifier.Text == calledMethod.Name);

          var location = invocationExpression.GetLocation();
          var lineSpan = location.GetLineSpan();

          var startLine = lineSpan.StartLinePosition.Line + 1;
          var startColumn = lineSpan.StartLinePosition.Character + 1;

          var filePath = location.SourceTree?.FilePath;
          var safeFilePath = filePath?.Replace("\\", "_").Replace(":", "_").Replace(".", "_");

          var usingsInOriginalFile = declarationOfCalledMethod.SyntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name.ToString())
            .ToList();
          var usings = usingsInOriginalFile
            .Distinct()
            .Where(u => u != "InspecTree")
            .ToList();
          var namespaceName = calledMethod.ContainingNamespace.ToDisplayString();
          var className = calledMethod.ContainingType.Name;

          var methodName = calledMethod.Name;
          var isStatic = declarationOfCalledMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
          var returnType = declarationOfCalledMethod.ReturnType.ToString();
          var parameters = declarationOfCalledMethod.ParameterList.Parameters
            .Select(p => new GeneratedParameter(
              parameterType: p.Type.ToString(),
              parameterName: p.Identifier.Text))
            .ToList();
          var containingType = declarationOfCalledMethod.FirstAncestorOrSelf<ClassDeclarationSyntax>();

          return new InterceptedInvocation(
            usings: usings,
            namespaceName: namespaceName,
            className: className,
            classAccessModifier: containingType.Modifiers.ToFullString().Trim(),
            line: startLine,
            column: startColumn,
            filePath: filePath,
            methodAccessModifier: declarationOfCalledMethod.Modifiers.ToFullString().Trim(),
            returnType: returnType,
            methodName: methodName,
            parameters: parameters,
            argumentList: invocationExpression.ArgumentList);
        });

      var compilationWithProviders = context.CompilationProvider
        .Combine(invocationProvider.Collect());

      context.RegisterSourceOutput(compilationWithProviders, (ctx, t) =>
      {
        var source = $@"
namespace System.Runtime.CompilerServices
{{
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
  sealed class InterceptsLocationAttribute : Attribute
  {{
    public InterceptsLocationAttribute(string filePath, int line, int character)
    {{
      _ = filePath;
      _ = line;
      _ = character;
    }}
  }}
}}
";
        ctx.AddSource("InterceptionExtensions.g.cs", SourceText.From(source, Encoding.UTF8));

        var invocationsToIntercept = t.Right
          .Where(i => i != null);

        var sourceFiles = _interceptedInvocationToSourceConverter.ConvertToSource(invocationsToIntercept.ToList());

        foreach (var sourceFile in sourceFiles)
          ctx.AddSource(sourceFile.FileName, sourceFile.SourceText);
      });
    }
  }
}

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
    private sealed class MethodToIntercept
    {
      public int Line { get; }
      public int Column { get; }
      public string FilePath { get; }
      public InvocationExpressionSyntax Invocation { get; }
      public string OutputFileName { get; set; }
      public Func<string, string> GeneratedStuff { get; set; }

      public MethodToIntercept(
        int line,
        int column,
        string filePath,
        InvocationExpressionSyntax invocation,
        string outputFileName,
        Func<string, string> generatedStuff)
      {
        Line = line;
        Column = column;
        FilePath = filePath;
        Invocation = invocation;
        OutputFileName = outputFileName;
        GeneratedStuff = generatedStuff;
      }
    }

    private (string, string) ConvertParameters(ParameterListSyntax parameterListSyntax, ArgumentListSyntax arguments)
    {
      var parameters = parameterListSyntax.Parameters.Select((p, i) => ConvertParameter(p, arguments.Arguments[i])).ToList();
      return (
        string.Join(", ", parameters.Select(s => s.Item1)),
        string.Join("\n", parameters.Select(s => s.Item2))
      );
    }

    private (string, string) ConvertParameter(ParameterSyntax parameterSyntax, ArgumentSyntax argumentSyntax)
    {
      if (parameterSyntax.Type.ToString().StartsWith("InspecTree<", StringComparison.InvariantCulture))
      {
        var typeString = parameterSyntax.Type.ToString();
        var withoutInspecTree = typeString.Substring("InspecTree<".Length, typeString.Length - "InspecTree<".Length - 1);

        var argumentString = argumentSyntax.Expression.ToString()
          .Replace("\"", "\"\"");

        return (
          $"{withoutInspecTree} {parameterSyntax.Identifier}",
$@"
var overload_{parameterSyntax.Identifier} = new InspecTree<{withoutInspecTree}>({parameterSyntax.Identifier},
@""
{argumentString}
"");");
      }

      return (
        $"{parameterSyntax.Type} {parameterSyntax.Identifier}",
        $"var overload_{parameterSyntax.Identifier} = {parameterSyntax.Identifier};"
      );
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
      var providerMethodDeclaration = context.SyntaxProvider.CreateSyntaxProvider(
        predicate: (node, _) => node is MethodDeclarationSyntax mds
                                && mds.ParameterList.Parameters
                                  .Any(p => p.Type.ToString()
                                    .StartsWith("InspecTree<", StringComparison.InvariantCulture)),
        transform: (syntaxContext, token) =>
        {
          return syntaxContext.Node as MethodDeclarationSyntax;
        });

      var methodDeclarationsFromProvider = providerMethodDeclaration.Collect();
      var methodDeclarations = methodDeclarationsFromProvider.Select((mds, t) => mds);

      var invocationProvider = context.SyntaxProvider.CreateSyntaxProvider(
        predicate: (node, _) => node is InvocationExpressionSyntax ies,
        transform: (syntaxContext, token) =>
        {
          var node = syntaxContext.Node as InvocationExpressionSyntax;
          var invokedMethodSymbolInfo = syntaxContext.SemanticModel.GetSymbolInfo(node.Expression, token);
          if (!(invokedMethodSymbolInfo.CandidateSymbols.FirstOrDefault() is IMethodSymbol calledMethod))
            return null;

          var declarationOfCalledMethod = syntaxContext.SemanticModel.Compilation.SyntaxTrees
            .SelectMany(st => st.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .FirstOrDefault(mds => mds.Identifier.Text == calledMethod.Name);

          var location = node.GetLocation();
          var lineSpan = location.GetLineSpan();

          var startLine = lineSpan.StartLinePosition.Line + 1;
          var startColumn = lineSpan.StartLinePosition.Character + 1;

          var filePath = location.SourceTree?.FilePath;
          var safeFilePath = filePath?.Replace("\\", "_").Replace(":", "_").Replace(".", "_");

          var namespaceName = calledMethod.ContainingNamespace.ToDisplayString();
          var className = calledMethod.ContainingType.Name;

          var methodName = calledMethod.Name;
          var isStatic = declarationOfCalledMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
          var returnType = declarationOfCalledMethod.ReturnType.ToString();
          var (parameters, body) = ConvertParameters(declarationOfCalledMethod.ParameterList, node.ArgumentList);
          var callMethod = $"{methodName}({string.Join(", ", declarationOfCalledMethod.ParameterList.Parameters.Select(p => $"overload_{p.Identifier}"))});";
          string generatedClass(string attrib)
          {
            return $@"
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace {namespaceName}
{{
  public partial class {className}
  {{
    {attrib}
    public{(isStatic ? " static" : "")} {returnType} {methodName}__INTERCEPTED_{safeFilePath}__{startLine}__{startColumn}({parameters})
    {{
      {body}
      {(returnType == "void" ? callMethod : $"return {callMethod}")}
    }}
  }}
}}";
          }

          return new MethodToIntercept(startLine, startColumn, filePath, node, $"{namespaceName}_{className}_{methodName}_INTERCEPT_{startLine}_{startColumn}.g.cs", generatedClass);
        });

      var compilationWithProviders = context.CompilationProvider
        .Combine(invocationProvider.Collect());

      context.RegisterSourceOutput(compilationWithProviders, (ctx, t) =>
      {
        var invocations = t.Right
          .Where(i => i != null);

        var source = $@"
using System;
using System.Runtime.CompilerServices;

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

        foreach (var invoc in invocations)
        {
          var attrib = $"[System.Runtime.CompilerServices.InterceptsLocation(@\"{invoc.FilePath}\", line: {invoc.Line}, character: {invoc.Column})]";
          var code = invoc.GeneratedStuff(attrib);
          ctx.AddSource(invoc.OutputFileName, SourceText.From(code, Encoding.UTF8));
        }
      });
    }
  }
}

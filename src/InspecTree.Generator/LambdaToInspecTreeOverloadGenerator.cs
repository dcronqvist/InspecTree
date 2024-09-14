using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InspecTree;

[Generator]
public class LambdaToInspecTreeOverloadGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context) =>
      context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());

    public void Execute(GeneratorExecutionContext context)
    {
        // Retrieve the collected methods
        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
            return;

        var methods = receiver.CandidateMethods;

        foreach (var method in methods)
        {
            var methodSymbol = context.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method);

            var invocationsToIntercept = receiver.CandidateInvocations
              .Where(i => i.ArgumentList.Arguments.Any(a => a.Expression is IdentifierNameSyntax ins && ins.Identifier.Text == methodSymbol.Name))
              .ToList();

            var namespaceName = methodSymbol.ContainingNamespace.ToDisplayString();
            var className = method.FirstAncestorOrSelf<ClassDeclarationSyntax>().Identifier.Text;

            var methodName = method.Identifier.Text;
            var sourceText = method.ToFullString();
            var isStatic = method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
            var returnType = method.ReturnType.ToString();
            var (parameters, _) = ConvertParameters(method.ParameterList);
            var generatedClass =
            $$"""
      using Microsoft.CodeAnalysis;
      using Microsoft.CodeAnalysis.CSharp;
      using Microsoft.CodeAnalysis.CSharp.Syntax;

      namespace {{namespaceName}}
      {
        public partial class {{className}}
        {
          public{{(isStatic ? " static" : "")}} {{returnType}} {{methodName}}({{parameters}})
          {
            /* Empty overload that can be intercepted */
          }
        }
      }
      """;
            context.AddSource($"{namespaceName}_{className}_{methodName}_Overload.g.cs", SourceText.From(generatedClass, Encoding.UTF8));
        }
    }

    private (string, string) ConvertParameters(ParameterListSyntax parameterListSyntax)
    {
        var parameters = parameterListSyntax.Parameters.Select(ConvertParameter).ToList();
        return (
          string.Join(", ", parameters.Select(s => s.Item1)),
          string.Join("\n", parameters.Select(s => s.Item2))
        );
    }

    private (string, string) ConvertParameter(ParameterSyntax parameterSyntax)
    {
        if (parameterSyntax.Type.ToString().StartsWith("InspecTree<"))
        {
            var typeString = parameterSyntax.Type.ToString();
            var withoutInspecTree = typeString.Substring("InspecTree<".Length, typeString.Length - "InspecTree<".Length - 1);

            return (
              $"{withoutInspecTree} {parameterSyntax.Identifier}",
              $"var overload_{parameterSyntax.Identifier} = new InspecTree<{withoutInspecTree}>({parameterSyntax.Identifier});"
            );
        }

        return (
          $"{parameterSyntax.Type} {parameterSyntax.Identifier}",
          $"var overload_{parameterSyntax.Identifier} = {parameterSyntax.Identifier};"
        );
    }

    private sealed class SyntaxReceiver : ISyntaxReceiver
    {
        public List<MethodDeclarationSyntax> CandidateMethods { get; } = new List<MethodDeclarationSyntax>();
        public List<InvocationExpressionSyntax> CandidateInvocations { get; } = new List<InvocationExpressionSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is MethodDeclarationSyntax methodDeclaration
               && methodDeclaration.ParameterList.Parameters.Count > 0
               && methodDeclaration.ParameterList.Parameters.Any(p => p.Type.ToString().Contains(nameof(InspecTree))))
            {
                CandidateMethods.Add(methodDeclaration);
                return;
            }

            if (syntaxNode is InvocationExpressionSyntax invocation
                && invocation.ArgumentList.Arguments.Count > 0)
            {
                var arguments = invocation.ArgumentList.Arguments;
                foreach (var argument in arguments)
                {
                    if (argument.Expression is ParenthesizedLambdaExpressionSyntax ples)
                    {
                        CandidateInvocations.Add(invocation);
                        return;
                    }
                }
            }
        }
    }
}

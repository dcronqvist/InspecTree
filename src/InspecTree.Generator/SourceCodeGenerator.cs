using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InspecTree;

[Generator]
public class InspecTreeGenerator : IIncrementalGenerator
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
    if (parameterSyntax.Type.ToString().StartsWith("InspecTree<", System.StringComparison.InvariantCulture))
    {
      var typeString = parameterSyntax.Type.ToString();
      var withoutInspecTree = typeString.Substring("InspecTree<".Length, typeString.Length - "InspecTree<".Length - 1);

      var argumentString = argumentSyntax.Expression.ToString();

      return (
        $"{withoutInspecTree} {parameterSyntax.Identifier}",
        $""""
        var overload_{parameterSyntax.Identifier} = new InspecTree<{withoutInspecTree}>({parameterSyntax.Identifier},
        """
        {argumentString}
        """);
        """"
      );
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
                                  .StartsWith("InspecTree<", System.StringComparison.InvariantCulture)),
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
        if (invokedMethodSymbolInfo.CandidateSymbols.FirstOrDefault() is not IMethodSymbol calledMethod)
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
        var generatedClass = (string attrib) =>
        $$"""
        using Microsoft.CodeAnalysis;
        using Microsoft.CodeAnalysis.CSharp;
        using Microsoft.CodeAnalysis.CSharp.Syntax;

        namespace {{namespaceName}}
        {
          public partial class {{className}}
          {
            {{attrib}}
            public{{(isStatic ? " static" : "")}} {{returnType}} {{methodName}}__INTERCEPTED_{{safeFilePath}}__{{startLine}}__{{startColumn}}({{parameters}})
            {
              {{body}}
              {{methodName}}({{string.Join(", ", declarationOfCalledMethod.ParameterList.Parameters.Select(p => $"overload_{p.Identifier}"))}});
            }
          }
        }
        """;

        return new MethodToIntercept(startLine, startColumn, filePath, node, $"{namespaceName}_{className}_{methodName}_INTERCEPT_{startLine}_{startColumn}.g.cs", generatedClass);
      });

    var compilationWithProviders = context.CompilationProvider
      .Combine(invocationProvider.Collect());

    context.RegisterSourceOutput(compilationWithProviders, (ctx, t) =>
    {
      var invocations = t.Right
        .Where(i => i != null);

      var source =
      $$"""
      // <auto-generated/>
      using System;
      using System.Runtime.CompilerServices;

      namespace System.Runtime.CompilerServices
      {
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        sealed class InterceptsLocationAttribute(string filePath, int line, int character) : Attribute { }
      }
      """;

      ctx.AddSource("InterceptionExtensions.g.cs", SourceText.From(source, Encoding.UTF8));

      foreach (var invoc in invocations)
      {
        var attrib = $$"""[System.Runtime.CompilerServices.InterceptsLocation(@"{{invoc.FilePath}}", line: {{invoc.Line}}, character: {{invoc.Column}})]""";
        var code = invoc.GeneratedStuff(attrib);
        ctx.AddSource(invoc.OutputFileName, SourceText.From(code, Encoding.UTF8));
      }
    });
  }
}

[Generator]
public class InspecTreeInvocationGenerator : ISourceGenerator
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
      var (parameters, body) = ConvertParameters(method.ParameterList);
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
            /* {{body}} */
            // {{methodName}}({{string.Join(", ", method.ParameterList.Parameters.Select(p => $"overload_{p.Identifier}"))}});
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

// public class SourceCodeGenerator : ISourceGenerator
// {
//   private class SyntaxReceiver : ISyntaxReceiver
//   {
//     public List<InvocationExpressionSyntax> CandidateInvocations { get; } = new List<InvocationExpressionSyntax>();

//     public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
//     {
//       // if (syntaxNode is MethodDeclarationSyntax methodDeclaration
//       //    && methodDeclaration.AttributeLists.Count > 0)
//       // {
//       //   foreach (var attributeList in methodDeclaration.AttributeLists)
//       //   {
//       //     foreach (var attribute in attributeList.Attributes)
//       //     {
//       //       var name = attribute.Name.ToString();
//       //       if (name.Contains(nameof(CaptureSyntaxAttribute).Replace("Attribute", "")))
//       //       {
//       //         CandidateMethods.Add(methodDeclaration);
//       //       }
//       //     }
//       //   }
//       // }

//       if (syntaxNode is InvocationExpressionSyntax invocation
//           && invocation.Expression is MemberAccessExpressionSyntax memberAccess
//           && memberAccess.Name.Identifier.Text == "GetSyntaxTree"
//           && memberAccess.Expression is IdentifierNameSyntax identifierName
//           && identifierName.Identifier.Text == "SyntaxProvider")
//       {
//         CandidateInvocations.Add(invocation);
//       }
//     }
//   }

//   private const string SyntaxProviderText = @"
// namespace System
// {
//   public static class SyntaxProvider
//   {
//     public static SyntaxTree GetSyntaxTree(Delegate @delegate)
//     {
//       return null;
//     }
//   }
// }
// ";

//   public void Initialize(GeneratorInitializationContext context) =>
//     // Register a syntax receiver to collect candidate methods
//     context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());

//   private string GetSourceForDelegateExpression(ExpressionSyntax expression, GeneratorExecutionContext context)
//   {
//     var semanticModel = context.Compilation.GetSemanticModel(expression.SyntaxTree);
//     if (expression is IdentifierNameSyntax ins)
//     {
//       var symbolInfo = semanticModel.GetSymbolInfo(ins);
//       var symbol = symbolInfo.CandidateSymbols.FirstOrDefault();
//       if (!(symbol is IMethodSymbol methodSymbol))
//         throw new ArgumentException($"Unable to get method symbol from expression: {expression}");

//       var methodDeclaration = methodSymbol.DeclaringSyntaxReferences[0].GetSyntax() as MethodDeclarationSyntax;
//       return methodDeclaration.ToFullString();
//     }
//     else if (expression is ParenthesizedLambdaExpressionSyntax ples)
//     {
//       return ples.ToFullString();
//     }

//     throw new ArgumentException($"Unable to get method symbol from expression: {expression}");
//   }

//   public void Execute(GeneratorExecutionContext context)
//   {
//     // Retrieve the collected methods
//     if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
//       return;

//     context.AddSource("SyntaxProvider.g.cs", SourceText.From(SyntaxProviderText, Encoding.UTF8));

//     foreach (var invocation in receiver.CandidateInvocations)
//     {
//       // Argument is a Delegate that represents a method
//       var delegateExpression = invocation.ArgumentList.Arguments[0].Expression;
//       var syntaxTree = GetSourceForDelegateExpression(delegateExpression, context);
//     }

//     //       foreach (var method in receiver.CandidateMethods)
//     //       {
//     //         var methodSymbol = context.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method);

//     //         var namespaceName = methodSymbol.ContainingNamespace.ToDisplayString();
//     //         var className = method.FirstAncestorOrSelf<ClassDeclarationSyntax>().Identifier.Text;

//     //         var methodName = method.Identifier.Text;
//     //         var sourceText = method.ToFullString();
//     //         var generatedClass = $@"
//     // using Microsoft.CodeAnalysis;
//     // using Microsoft.CodeAnalysis.CSharp;
//     // using Microsoft.CodeAnalysis.CSharp.Syntax;

//     // namespace {namespaceName}
//     // {{
//     //   public partial class {className}
//     //   {{
//     //     public const string {methodName}Source = @""{EscapeString(sourceText)}"";
//     //   }}
//     // }}";
//     //         context.AddSource($"{namespaceName}_{className}_{methodName}_Source.g.cs", SourceText.From(generatedClass, Encoding.UTF8));
//     //       }
//   }

//   private string EscapeString(string str) => str.Replace("\"", "\"\"");

//   private sealed class SyntaxWalker : CSharpSyntaxWalker
//   {
//     private readonly StringBuilder _builder = new StringBuilder();
//     private readonly GeneratorExecutionContext _context;

//     public SyntaxWalker(GeneratorExecutionContext context) : base(SyntaxWalkerDepth.Trivia)
//     {
//       _context = context;
//     }

//     public override string ToString()
//     {
//       Line(")");
//       return $"SyntaxFactory.SyntaxTree({_builder}";
//     }

//     private int _currentIndentation = 6;
//     private void Line(string line)
//     {
//       _builder.AppendLine();
//       _builder.Append(new string(' ', _currentIndentation) + line);
//     }

//     private void LinesWithSeparator(string separator, params string[] lines)
//     {
//       for (var i = 0; i < lines.Length; i++)
//       {
//         Line(lines[i] + (i < lines.Length - 1 ? separator : ""));
//       }
//     }
//     private void LinesWithSeparator(string separator, IEnumerable<string> lines) => LinesWithSeparator(separator, lines.ToArray());
//     private void Text(string text) => _builder.Append(text);
//     private void Indent() => _currentIndentation += 2;
//     private void Unindent() => _currentIndentation -= 2;

//     public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
//     {
//       // var parenthesizedLambdaExpression = SyntaxFactory.ParenthesizedLambdaExpression(
//       //    node.ReturnType,
//       //    "RandomName")
//       //    .WithParameterList(node.ParameterList)
//       //    .WithBody(node.Body)
//       //    .WithExpressionBody(node.ExpressionBody)
//       //    .WithAttributeLists(node.AttributeLists)

//       Line("SyntaxFactory.ParenthesizedLambdaExpression(");
//       Indent();
//       Line($"{ConvertTypeSyntaxToString(node.ReturnType)},");
//       Line($"\"RandomName\"");
//       Unindent();
//       Line(")");

//       base.VisitParenthesizedLambdaExpression(node);
//     }

//     public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
//     {
//       var methodDeclaration = SyntaxFactory.MethodDeclaration(
//          node.ReturnType,
//          node.Identifier)
//         .WithModifiers(node.Modifiers)
//         .WithParameterList(node.ParameterList)
//         .WithBody(node.Body)
//         .WithExpressionBody(node.ExpressionBody)
//         .WithAttributeLists(node.AttributeLists);

//       Line("SyntaxFactory.MethodDeclaration(");
//       Indent();
//       Line(ConvertTypeSyntaxToString(node.ReturnType) + ",");
//       Line($"\"{node.Identifier}\")");
//       Line($".WithModifiers(");
//       Indent();
//       VisitSyntaxTokenList(node.Modifiers);
//       Unindent();
//       Line(")");

//       if (node.ParameterList.Parameters.Count > 0)
//       {
//         Line($".WithParameterList(");
//         Indent();
//         node.ParameterList.Accept(this);
//         Unindent();
//         Line(")");
//       }

//       if (node.Body != null)
//       {
//         Line($".WithBody(");
//         Indent();
//         node.Body.Accept(this);
//         Unindent();
//         Line(")");
//       }

//       Unindent();
//     }

//     public void VisitSyntaxTokenList(SyntaxTokenList tokenList)
//     {
//       Line("SyntaxFactory.TokenList(");
//       Indent();
//       LinesWithSeparator(",", tokenList.Select(ConvertSyntaxTokenToString));
//       Unindent();
//       Line(")");
//     }

//     public override void VisitParameterList(ParameterListSyntax syntax)
//     {
//       var x = SyntaxFactory.ParameterList(
//         SyntaxFactory.SeparatedList<ParameterSyntax>(
//           syntax.Parameters
//         )
//       );

//       // ---

//       Line("SyntaxFactory.ParameterList(");
//       Indent();
//       Line("SyntaxFactory.SeparatedList<ParameterSyntax>(");
//       Indent();
//       for (var i = 0; i < syntax.Parameters.Count; i++)
//       {
//         syntax.Parameters[i].Accept(this);
//         if (i < syntax.Parameters.Count - 1)
//           Text(",");
//       }
//       Unindent();
//       Line(")");
//     }

//     public override void VisitParameter(ParameterSyntax node)
//     {
//       var x = SyntaxFactory.Parameter(
//         node.Identifier
//       );

//       // ---

//       Line("SyntaxFactory.Parameter(");
//       Indent();
//       Line($"{node.Identifier}");
//       Unindent();
//       Line(")");

//       base.VisitParameter(node);
//     }

//     public override void VisitBlock(BlockSyntax node)
//     {
//       var statements = node.Statements;

//       var x = SyntaxFactory.Block(
//         SyntaxFactory.Token(SyntaxKind.OpenBraceToken),
//         SyntaxFactory.List<StatementSyntax>(statements),
//         SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
//       );

//       // ---

//       Line("SyntaxFactory.Block(");
//       Indent();
//       Line(ConvertSyntaxTokenToString(SyntaxFactory.Token(SyntaxKind.OpenBraceToken)) + ",");

//       if (statements.Count == 0)
//       {
//         Line("SyntaxFactory.List<StatementSyntax>(),");
//       }
//       else
//       {
//         Line("SyntaxFactory.List<StatementSyntax>(new List<StatementSyntax> {");
//         Indent();
//         AcceptAsCommaSeparatedListOnLines(statements);
//         Unindent();
//         Line("}),");
//       }
//       Line(ConvertSyntaxTokenToString(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)));
//       Unindent();
//       Line(")");
//     }

//     public override void VisitExpressionStatement(ExpressionStatementSyntax node)
//     {
//       var x = SyntaxFactory.ExpressionStatement(
//         node.Expression
//       );

//       // ---

//       Line("SyntaxFactory.ExpressionStatement(");
//       Indent();
//       node.Expression.Accept(this);
//       Unindent();
//       Line(")");
//     }

//     public override void VisitInvocationExpression(InvocationExpressionSyntax node)
//     {
//       var x = SyntaxFactory.InvocationExpression(
//         node.Expression,
//         node.ArgumentList
//       );

//       // ---

//       Line("SyntaxFactory.InvocationExpression(");
//       Indent();
//       node.Expression.Accept(this);
//       Text(",");
//       node.ArgumentList.Accept(this);
//       Unindent();
//       Line(")");
//     }

//     public override void VisitArgumentList(ArgumentListSyntax node)
//     {
//       var x = SyntaxFactory.ArgumentList(
//         SyntaxFactory.SeparatedList<ArgumentSyntax>(
//           node.Arguments
//         )
//       );

//       // ---

//       Line("SyntaxFactory.ArgumentList(");
//       Indent();
//       Line("SyntaxFactory.SeparatedList<ArgumentSyntax>(new List<ArgumentSyntax> {");
//       Indent();
//       AcceptAsCommaSeparatedListOnLines(node.Arguments);
//       Unindent();
//       Line("})");
//       Unindent();
//       Line(")");
//     }

//     public override void VisitArgument(ArgumentSyntax node)
//     {
//       var x = SyntaxFactory.Argument(
//         node.Expression
//       );

//       // ---

//       Line("SyntaxFactory.Argument(");
//       Indent();
//       node.Expression.Accept(this);
//       Unindent();
//       Line(")");

//     }

//     public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
//     {
//       var x = SyntaxFactory.MemberAccessExpression(
//         node.Kind(),
//         node.Expression,
//         node.OperatorToken,
//         node.Name
//       );

//       // ---

//       Line("SyntaxFactory.MemberAccessExpression(");
//       Indent();
//       Line($"SyntaxKind.{node.Kind()},");
//       node.Expression.Accept(this);
//       Text(",");
//       Line(ConvertSyntaxTokenToString(node.OperatorToken) + ",");
//       node.Name.Accept(this);
//       Unindent();
//       Line(")");
//     }

//     public override void VisitIdentifierName(IdentifierNameSyntax node)
//     {
//       var x = SyntaxFactory.IdentifierName(
//         node.Identifier
//       );

//       // ---

//       Line($"SyntaxFactory.IdentifierName(\"{node.Identifier}\")");
//     }

//     public override void VisitLiteralExpression(LiteralExpressionSyntax node)
//     {
//       if (node.Kind() == SyntaxKind.StringLiteralExpression)
//       {
//         var x = SyntaxFactory.LiteralExpression(
//           SyntaxKind.StringLiteralExpression,
//           SyntaxFactory.Literal(node.Token.ValueText)
//         );

//         // ---

//         Line("SyntaxFactory.LiteralExpression(");
//         Indent();
//         Line($"SyntaxKind.{node.Kind()},");
//         Line($"SyntaxFactory.Literal(\"{node.Token.ValueText}\")");
//         Unindent();
//         Line(")");
//       }
//       else if (node.Kind() == SyntaxKind.TrueLiteralExpression)
//       {
//         var x = SyntaxFactory.LiteralExpression(
//           SyntaxKind.TrueLiteralExpression
//         );

//         // ---

//         Line("SyntaxFactory.LiteralExpression(");
//         Indent();
//         Line($"SyntaxKind.{node.Kind()}");
//         Unindent();
//         Line(")");
//       }
//       else if (node.Kind() == SyntaxKind.FalseLiteralExpression)
//       {
//         var x = SyntaxFactory.LiteralExpression(
//           SyntaxKind.FalseLiteralExpression
//         );

//         // ---

//         Line("SyntaxFactory.LiteralExpression(");
//         Indent();
//         Line($"SyntaxKind.{node.Kind()}");
//         Unindent();
//         Line(")");
//       }
//       else
//       {
//         base.VisitLiteralExpression(node);
//       }
//     }

//     public override void VisitIfStatement(IfStatementSyntax node)
//     {
//       var x = SyntaxFactory.IfStatement(
//         node.Condition,
//         node.Statement,
//         node.Else
//       );

//       // ---

//       Line("SyntaxFactory.IfStatement(");
//       Indent();
//       node.Condition.Accept(this);
//       Text(",");
//       node.Statement.Accept(this);
//       Text(",");
//       if (node.Else != null)
//       {
//         node.Else.Accept(this);
//       }
//       else
//       {
//         Line("null");
//       }
//       Unindent();
//       Line(")");
//     }

//     // ---------- Helpers ----------

//     public static string ConvertTypeSyntaxToString(TypeSyntax node) => $"SyntaxFactory.ParseTypeName(\"{node}\")";

//     public static string ConvertSyntaxTokenToString(SyntaxToken token) =>
//       $"SyntaxFactory.Token(SyntaxKind.{token.Kind()})";

//     public void AcceptAsCommaSeparatedListOnLines<T>(IEnumerable<T> nodes) where T : CSharpSyntaxNode
//     {
//       foreach (var node in nodes)
//       {
//         node.Accept(this);
//         Text(",");
//       }

//       if (nodes.Any())
//       {
//         _builder.Remove(_builder.Length - 1, 1);
//       }
//     }
//   }
// }

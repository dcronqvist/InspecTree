using InspecTree.Generator;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace InspecTree.Tests;

public class InterceptedInvocationToSourceConverterTests
{
  [Fact]
  public void ConvertToSource_Always_GeneratesOneFilePerInterceptedInvocation()
  {
    // Arrange
    var method1 = new InterceptedInvocation(
      usings: [],
      namespaceName: "InspecTreeTestInterceptors",
      className: "InspecTreeInterceptors",
      classAccessModifier: "public partial",
      line: 1,
      column: 1,
      filePath: "C:/test.cs",
      methodAccessModifier: "public static",
      returnType: "void",
      methodName: "TestMethod",
      parameters: [
        new GeneratedParameter(
          parameterType: "int",
          parameterName: "x"
        )
      ],
      argumentList: SyntaxFactory.ArgumentList([
        SyntaxFactory.Argument(
          SyntaxFactory.LiteralExpression(
            SyntaxKind.NumericLiteralExpression,
            SyntaxFactory.Literal(1)
          )
        )
      ])
    );
    var method2 = new InterceptedInvocation(
      usings: [],
      namespaceName: "InspecTreeTestInterceptors",
      className: "InspecTreeInterceptors",
      classAccessModifier: "public partial",
      line: 2,
      column: 2,
      filePath: "C:\\test2.cs",
      methodAccessModifier: "public",
      returnType: "void",
      methodName: "TestMethod2",
      parameters: [
        new GeneratedParameter(
          parameterType: "int",
          parameterName: "x"
        )
      ],
      argumentList: SyntaxFactory.ArgumentList([
        SyntaxFactory.Argument(
          SyntaxFactory.LiteralExpression(
            SyntaxKind.NumericLiteralExpression,
            SyntaxFactory.Literal(1)
          )
        )
      ])
    );
    var converter = new InterceptedInvocationToSourceConverter();

    // Act
    var result = converter.ConvertToSource([method1, method2]).ToList();

    // Assert
    Assert.Equal(2, result.Count);
    Assert.Equal("Intercepted_TestMethod_C__test_cs_1_1.g.cs", result[0].FileName);
    Assert.Equal("Intercepted_TestMethod2_C__test2_cs_2_2.g.cs", result[1].FileName);
  }

  [Fact]
  public void ConvertToSource_NotStaticSingleParameter_GeneratesCorrectSource()
  {
    // Arrange
    var method = new InterceptedInvocation(
      usings: [
        "System"
      ],
      namespaceName: "InspecTreeTestInterceptors",
      className: "InspecTreeInterceptors",
      classAccessModifier: "public partial",
      line: 1,
      column: 1,
      filePath: "C:\\test.cs",
      methodAccessModifier: "public",
      returnType: "void",
      methodName: "TestMethod",
      parameters: [
        new GeneratedParameter(
          parameterType: "int",
          parameterName: "x"
        )
      ],
      argumentList: SyntaxFactory.ArgumentList([
        SyntaxFactory.Argument(
          SyntaxFactory.LiteralExpression(
            SyntaxKind.NumericLiteralExpression,
            SyntaxFactory.Literal(1)
          )
        )
      ])
    );
    var converter = new InterceptedInvocationToSourceConverter();

    // Act
    var result = converter.ConvertToSource([method]).ToList();

    // Assert
    var file = Assert.Single(result);
    Assert.Equal("Intercepted_TestMethod_C__test_cs_1_1.g.cs", file.FileName);
    Assert.Equal(
      """
      using System;

      namespace InspecTreeTestInterceptors
      {
        public partial class InspecTreeInterceptors
        {
          [System.Runtime.CompilerServices.InterceptsLocation(@"C:\test.cs", line: 1, character: 1)]
          public void TestMethod__INTERCEPTED_C__test_cs_1_1(int x)
          {
            var overload_x = x;
            TestMethod(overload_x);
          }
        }
      }
      """, file.SourceText.ToString());
  }

  [Fact]
  public void ConvertToSource_StaticMultipleParameters_GeneratesCorrectSource()
  {
    // Arrange
    var method = new InterceptedInvocation(
      usings: [
        "System",
        "System.Linq"
      ],
      namespaceName: "InspecTreeTestInterceptors",
      className: "InspecTreeInterceptors",
      classAccessModifier: "public partial",
      line: 1,
      column: 1,
      filePath: "C:\\test.cs",
      methodAccessModifier: "public static",
      returnType: "int",
      methodName: "TestMethod",
      parameters: [
        new GeneratedParameter(
          parameterType: "int",
          parameterName: "x"
        ),
        new GeneratedParameter(
          parameterType: "string",
          parameterName: "y"
        )
      ],
      argumentList: SyntaxFactory.ArgumentList([
        SyntaxFactory.Argument(
          SyntaxFactory.LiteralExpression(
            SyntaxKind.NumericLiteralExpression,
            SyntaxFactory.Literal(1)
          )
        ),
        SyntaxFactory.Argument(
          SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal("test")
          )
        )
      ])
    );
    var converter = new InterceptedInvocationToSourceConverter();

    // Act
    var result = converter.ConvertToSource([method]).ToList();

    // Assert
    var file = Assert.Single(result);
    Assert.Equal("Intercepted_TestMethod_C__test_cs_1_1.g.cs", file.FileName);
    Assert.Equal(
      """
      using System;
      using System.Linq;

      namespace InspecTreeTestInterceptors
      {
        public partial class InspecTreeInterceptors
        {
          [System.Runtime.CompilerServices.InterceptsLocation(@"C:\test.cs", line: 1, character: 1)]
          public static int TestMethod__INTERCEPTED_C__test_cs_1_1(int x, string y)
          {
            var overload_x = x;
            var overload_y = y;
            return TestMethod(overload_x, overload_y);
          }
        }
      }
      """, file.SourceText.ToString());
  }

  [Fact]
  public void ConvertToSource_ParameterIsInspecTreeWithSimpleLambda_GeneratesCorrectSource()
  {
    // Arrange
    var invocation = SyntaxFactory.ParseSyntaxTree("TestMethod(x => x + 1)");
    var invocationMembers = (invocation.GetRoot() as CompilationUnitSyntax).Members;
    var invocationSyntax = ((invocationMembers[0] as GlobalStatementSyntax).Statement as ExpressionStatementSyntax).Expression as InvocationExpressionSyntax;
    var method = new InterceptedInvocation(
      usings: [
        "System"
      ],
      namespaceName: "InspecTreeTestInterceptors",
      className: "InspecTreeInterceptors",
      classAccessModifier: "public partial",
      line: 1,
      column: 1,
      filePath: "test.cs",
      methodAccessModifier: "public",
      returnType: "void",
      methodName: "TestMethod",
      parameters: [
        new GeneratedParameter(
          parameterType: "InspecTree<Func<int, int>>",
          parameterName: "insp"
        )
      ],
      argumentList: invocationSyntax.ArgumentList
    );
    var converter = new InterceptedInvocationToSourceConverter();

    // Act
    var result = converter.ConvertToSource([method]).ToList();

    // Assert
    var file = Assert.Single(result);
    Assert.Equal("Intercepted_TestMethod_test_cs_1_1.g.cs", file.FileName);
    Assert.Equal(
      """
      using System;

      namespace InspecTreeTestInterceptors
      {
        public partial class InspecTreeInterceptors
        {
          [System.Runtime.CompilerServices.InterceptsLocation(@"test.cs", line: 1, character: 1)]
          public void TestMethod__INTERCEPTED_test_cs_1_1(Func<int, int> insp)
          {
            var overload_insp_source = @"
            using System;

            var overload_insp_lambda = x => x + 1
            ;";
            var overload_insp_syntaxTree = CSharpSyntaxTree.ParseText(overload_insp_source);
            var overload_insp_lambdaDeclaration = overload_insp_syntaxTree.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>().Single(x =>
              x.Variables.Single().Identifier.Text == "overload_insp_lambda");
            var overload_insp_lambdaExpression = overload_insp_lambdaDeclaration.DescendantNodes().OfType<LambdaExpressionSyntax>().Single();
            var overload_insp_compilation = CSharpCompilation.Create("overload_insp")
              .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
              .AddSyntaxTrees(overload_insp_syntaxTree);
            var overload_insp_semanticModel = overload_insp_compilation.GetSemanticModel(overload_insp_syntaxTree);
            var overload_insp = new InspecTree<Func<int, int>>(
              insp,
              overload_insp_lambdaExpression,
              overload_insp_semanticModel);
            TestMethod(overload_insp);
          }
        }
      }
      """, file.SourceText.ToString());
  }

  [Fact]
  public void ConvertToSource_ParameterIsInspecTreeWithComplexLambda_GeneratesCorrectSource()
  {
    // Arrange
    var invocation = SyntaxFactory.ParseSyntaxTree(
      """
      TestMethod(x =>
      {
        if (x > 0)
          return x + 1;

        if (x < 0)
          return x - 1;

        return x;
      })
      """);
    var invocationMembers = (invocation.GetRoot() as CompilationUnitSyntax).Members;
    var invocationSyntax = ((invocationMembers[0] as GlobalStatementSyntax).Statement as ExpressionStatementSyntax).Expression as InvocationExpressionSyntax;
    var method = new InterceptedInvocation(
      usings: [
        "System"
      ],
      namespaceName: "InspecTreeTestInterceptors",
      className: "InspecTreeInterceptors",
      classAccessModifier: "public partial",
      line: 1,
      column: 1,
      filePath: "test.cs",
      methodAccessModifier: "public",
      returnType: "void",
      methodName: "TestMethod",
      parameters: [
        new GeneratedParameter(
          parameterType: "InspecTree<Func<int, int>>",
          parameterName: "insp"
        )
      ],
      argumentList: invocationSyntax.ArgumentList
    );
    var converter = new InterceptedInvocationToSourceConverter();

    // Act
    var result = converter.ConvertToSource([method]).ToList();

    // Assert
    var file = Assert.Single(result);
    Assert.Equal("Intercepted_TestMethod_test_cs_1_1.g.cs", file.FileName);
    Assert.Equal(
      """
      using System;

      namespace InspecTreeTestInterceptors
      {
        public partial class InspecTreeInterceptors
        {
          [System.Runtime.CompilerServices.InterceptsLocation(@"test.cs", line: 1, character: 1)]
          public void TestMethod__INTERCEPTED_test_cs_1_1(Func<int, int> insp)
          {
            var overload_insp_source = @"
            using System;

            var overload_insp_lambda = x =>
            {
              if (x > 0)
                return x + 1;

              if (x < 0)
                return x - 1;

              return x;
            }
            ;";
            var overload_insp_syntaxTree = CSharpSyntaxTree.ParseText(overload_insp_source);
            var overload_insp_lambdaDeclaration = overload_insp_syntaxTree.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>().Single(x =>
              x.Variables.Single().Identifier.Text == "overload_insp_lambda");
            var overload_insp_lambdaExpression = overload_insp_lambdaDeclaration.DescendantNodes().OfType<LambdaExpressionSyntax>().Single();
            var overload_insp_compilation = CSharpCompilation.Create("overload_insp")
              .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
              .AddSyntaxTrees(overload_insp_syntaxTree);
            var overload_insp_semanticModel = overload_insp_compilation.GetSemanticModel(overload_insp_syntaxTree);
            var overload_insp = new InspecTree<Func<int, int>>(
              insp,
              overload_insp_lambdaExpression,
              overload_insp_semanticModel);
            TestMethod(overload_insp);
          }
        }
      }
      """, file.SourceText.ToString());
  }
}

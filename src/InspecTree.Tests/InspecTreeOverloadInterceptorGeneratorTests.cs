using System.Collections.Immutable;
using System.Text;
using InspecTree.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NSubstitute;

namespace InspecTree.Tests;

public class InspecTreeOverloadInterceptorGeneratorTests
{
  // Custom AnalyzerConfigOptionsProvider to simulate build properties
  private sealed class CustomAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
  {
    private readonly AnalyzerConfigOptions _globalOptions;

    public CustomAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> options)
    {
      _globalOptions = new CustomAnalyzerConfigOptions(options);
    }

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new CustomAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new CustomAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);
  }

  // Custom AnalyzerConfigOptions to pass build properties
  private sealed class CustomAnalyzerConfigOptions : AnalyzerConfigOptions
  {
    private readonly ImmutableDictionary<string, string> _options;

    public CustomAnalyzerConfigOptions(ImmutableDictionary<string, string> options)
    {
      _options = options;
    }

    public override bool TryGetValue(string key, out string value) => _options.TryGetValue(key, out value);
  }

  private static (Compilation, GeneratorDriverRunResult, List<InterceptedInvocation>) TestSetup(
    string source)
  {
    var realConverter = new InterceptedInvocationToSourceConverter();
    var converter = Substitute.For<IInterceptedInvocationToSourceConverter>();
    converter.ConvertToSource(
      Arg.Any<IReadOnlyCollection<InterceptedInvocation>>())
      .Returns(c => realConverter.ConvertToSource(c.Arg<IReadOnlyCollection<InterceptedInvocation>>()));

    var generator = new InspecTreeOverloadInterceptorGenerator(converter);

    var analyzerOptions = ImmutableDictionary<string, string>.Empty
            .Add("InterceptorsPreviewNamespaces", "TestProject");

    var analyzerConfigOptionsProvider = new CustomAnalyzerConfigOptionsProvider(analyzerOptions);
    var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>();
    var optionsProvider = analyzerConfigOptionsProvider;

    var compilation = CSharpCompilation.Create("CSharpCodeGen.GenerateAssembly")
      .AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), path: "TestFile.cs"))
      .AddReferences(
        MetadataReference.CreateFromFile(Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
          ".nuget",
          "packages",
          "netstandard.library",
          "2.0.3",
          "build",
          "netstandard2.0",
          "ref",
          "netstandard.dll"
        )),
        MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(CSharpSyntaxTree).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(InspecTree<>).Assembly.Location))
      .WithOptions(
        new CSharpCompilationOptions(
          outputKind: OutputKind.DynamicallyLinkedLibrary));

    var driver = CSharpGeneratorDriver.Create([generator])
      .AddGenerators([new LambdaToInspecTreeOverloadGenerator()])
      .WithUpdatedAnalyzerConfigOptions(analyzerConfigOptionsProvider)
      .RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var _);

    var runResult = driver.GetRunResult();

    var receivedInterceptedInvocations = converter.ReceivedCalls()
      .Where(c => c.GetMethodInfo().Name == "ConvertToSource")
      .Select(c => c.GetArguments()[0] as IReadOnlyCollection<InterceptedInvocation>)
      .ToList();

    var interceptedInvocations = Assert.Single(receivedInterceptedInvocations);

    return (updatedCompilation, runResult, interceptedInvocations.ToList());
  }

  [Fact]
  public void InterceptorGenerator_Usings_InterceptedInvocationContainsOnlyOriginalFileUsings()
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      """
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {
          Test(x => x);
        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    var intercept = Assert.Single(intercepts);
    Assert.Single(intercept.Usings, u => u == "System");
  }

  [Theory]
  [InlineData("public partial", "public partial")]
  [InlineData("internal partial", "internal partial")]
  public void InterceptorGenerator_ClassAccessModifier_InterceptedInvocationHasExpectedAccessModifier(
    string accessInSource,
    string expectedAccessInGenerated)
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      {{accessInSource}} class Program
      {
        public static void Main(string[] _)
        {
          Test(x => x);
        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    var intercept = Assert.Single(intercepts);
    Assert.Equal(expectedAccessInGenerated, intercept.ClassAccessModifier);
  }

  [Theory]
  [InlineData("TestProjectNamespace", "TestProjectNamespace")]
  [InlineData("TestProject.Namespace", "TestProject.Namespace")]
  [InlineData("TestProject.Namespace.SubNamespace", "TestProject.Namespace.SubNamespace")]
  public void InterceptorGenerator_Namespace_IsCorrect(
    string namespaceInSource,
    string expectedNamespace)
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace {{namespaceInSource}};

      public partial class Program
      {
        public static void Main(string[] _)
        {
          Test(x => x);
        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    var intercept = Assert.Single(intercepts);
    Assert.Equal(expectedNamespace, intercept.NamespaceName);
  }

  [Theory]
  [InlineData("Program", "Program")]
  [InlineData("Program1", "Program1")]
  [InlineData("Program2", "Program2")]
  public void InterceptorGenerator_ClassName_IsCorrect(
    string classNameInSource,
    string expectedClassName)
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class {{classNameInSource}}
      {
        public static void Main(string[] _)
        {
          Test(x => x);
        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    var intercept = Assert.Single(intercepts);
    Assert.Equal(expectedClassName, intercept.ClassName);
  }

  [Fact]
  public void InterceptorGenerator_Location_IsCorrect()
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      """
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {
          Test(x => x);
        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    var intercept = Assert.Single(intercepts);
    Assert.Equal(10, intercept.Line);
    Assert.Equal(5, intercept.Column);
    Assert.Equal("TestFile.cs", intercept.FilePath);
  }

  [Theory]
  [InlineData("public static", "public static")]
  [InlineData("public", "public")]
  [InlineData("private", "private")]
  [InlineData("protected", "protected")]
  [InlineData("internal", "internal")]
  [InlineData("protected internal", "protected internal")]
  [InlineData("private protected", "private protected")]
  public void InterceptorGenerator_MethodAccessModifier_IsCorrect(
    string accessInSource,
    string expectedAccessInGenerated)
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {
          Test(x => x);
        }

        {{accessInSource}} void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    var intercept = Assert.Single(intercepts);
    Assert.Equal(expectedAccessInGenerated, intercept.MethodAccessModifier);
  }

  [Theory]
  [InlineData("int", "int")]
  [InlineData("string", "string")]
  [InlineData("bool", "bool")]
  [InlineData("void", "void")]
  [InlineData("Program", "Program")]
  public void InterceptorGenerator_ReturnType_IsCorrect(
    string returnTypeInSource,
    string expectedReturnType)
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {
          Test(x => x);
        }

        public static {{returnTypeInSource}} Test(InspecTree<Func<int, int>> insp)
        {
          {{(returnTypeInSource == "void" ? "return;" : "return default;")}}
        }
      }
      """);

    // Assert
    var intercept = Assert.Single(intercepts);
    Assert.Equal(expectedReturnType, intercept.ReturnType);
  }

  [Theory]
  [InlineData("Test", "Test")]
  [InlineData("Test1", "Test1")]
  [InlineData("Test2", "Test2")]
  public void InterceptorGenerator_MethodName_IsCorrect(
    string methodNameInSource,
    string expectedMethodName)
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {
          {{methodNameInSource}}(x => x);
        }

        public static void {{methodNameInSource}}(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    var intercept = Assert.Single(intercepts);
    Assert.Equal(expectedMethodName, intercept.MethodName);
  }

  [Fact]
  public void InterceptorGenerator_NoInvocationToOverloadOfInspecTreeMethod_NoInterceptedInvocations()
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      """
      using System;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {
          Test2(5);
          Test3(x => x + 2);
        }

        public static void Test(Func<int, int> insp)
        {
          return;
        }

        public static void Test2(int x)
        {
          return;
        }

        public static void Test3(Func<int, int> insp) => insp(5);
      }
      """);

    // Assert
    Assert.Empty(intercepts);
  }

  [Fact]
  public void InterceptorGenerator_OneInvocationToMethodWithInspecTreeParameter_OneInterceptedInvocation()
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      """
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {
          Test2(x => x);
          Test2(y => y * 2);
          Test(x => 3 * x);
        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }

        public static void Test2(Func<int, int> insp)
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Single(intercepts);
  }

  [Fact]
  public void InterceptorGenerator_MultipleInvocationsToMethodWithInspecTreeParameter_MultipleInterceptedInvocations()
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      """
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {
          Test2(x => x);
          Test2(y => y * 2);
          Test(x => 3 * x);
          Test(x => x * 10);
          Test(x => x * 100);
        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }

        public static void Test2(Func<int, int> insp)
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Equal(3, intercepts.Count);
  }

  [Fact]
  public void InterceptorGenerator_MethodHasInspecTreeParamtersAndIsInvoked_InterceptedInvocationParametersAndArgumentsIsCorrect()
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {
          Test(x => x);
        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    var intercept = Assert.Single(intercepts);

    var parameter = Assert.Single(intercept.Parameters);
    Assert.Equal("InspecTree<Func<int, int>>", parameter.ParameterType);
    Assert.Equal("insp", parameter.ParameterName);

    var argument = Assert.Single(intercept.ArgumentList.Arguments);
    Assert.Equal("x => x", argument.ToFullString());
  }

  [Fact]
  public void Test1()
  {
    // Arrange & Act
    var (_, _, intercepts) = TestSetup(
      $$"""
      using System;
      using System.Collections.Generic;
      using System.Linq;
      using System.Numerics;
      using System.Text;
      using Microsoft.CodeAnalysis;
      using Microsoft.CodeAnalysis.CSharp;
      using Microsoft.CodeAnalysis.CSharp.Syntax;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {
          string glslCode = TranspileCSharpToGLSLFragmentShader(() =>
          {
            float x = 2 + 5;
            Vector4 color = new Vector4(1.0f);

            if (x > 4)
            {
              color = new Vector4(0.2f, 0.3f, x, 1.0f);
            }

            return color;
          });

          Console.WriteLine(glslCode);

          // Produces: (with proper whitespace!)
          /*
          void main() {
            float x = (2 + 5);
            vec4 color = vec4(1);

            if ((x > 4)) {
              color = vec4(0.2, 0.3, x, 1);
            }

            FragColor = color;
            return;
          }
          */
        }

        public static string TranspileCSharpToGLSLFragmentShader(InspecTree<Func<Vector4>> insp)
        {
          return "";
        }
      }
      """);

    // Assert
    Assert.True(true);
  }
}

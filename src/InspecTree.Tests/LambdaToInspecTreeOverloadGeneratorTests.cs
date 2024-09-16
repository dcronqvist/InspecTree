using InspecTree.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSubstitute;

namespace InspecTree.Tests;

public class LambdaToInspecTreeOverloadGeneratorTests
{
  private static (Compilation, GeneratorDriverRunResult, List<GeneratedOverload>) TestSetup(
    string source)
  {
    var realConverter = new OverloadToSourceConverter();
    var converter = Substitute.For<IOverloadToSourceConverter>();
    converter.ConvertToSource(
      Arg.Any<IReadOnlyCollection<GeneratedOverload>>())
      .Returns(c => realConverter.ConvertToSource(c.Arg<IReadOnlyCollection<GeneratedOverload>>()));

    var generator = new LambdaToInspecTreeOverloadGenerator(converter);

    var compilation = CSharpCompilation.Create("CSharpCodeGen.GenerateAssembly")
      .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source))
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
        // MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(CSharpSyntaxTree).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(InspecTree<>).Assembly.Location))
      .WithOptions(
        new CSharpCompilationOptions(
          outputKind: OutputKind.DynamicallyLinkedLibrary));

    var driver = CSharpGeneratorDriver.Create([generator])
      .RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var _);

    var runResult = driver.GetRunResult();

    var receivedOverloads = converter.ReceivedCalls()
      .Where(c => c.GetMethodInfo().Name == "ConvertToSource")
      .Select(c => c.GetArguments()[0] as IReadOnlyCollection<GeneratedOverload>)
      .ToList();

    var overloads = Assert.Single(receivedOverloads);

    return (updatedCompilation, runResult, overloads.ToList());
  }

  [Fact]
  public void OverloadGenerator_MissingPartialOnClass_FailsCompilation()
  {
    // Arrange & Act
    var (compilation, _, _) = TestSetup(
      """
      using System;
      using InspecTree;

      namespace TestProject;

      public class Program
      {
        public static void Main(string[] _)
        {

        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    var diagnostics = compilation.GetDiagnostics();
    Assert.NotEmpty(diagnostics);
    Assert.Contains(diagnostics, d => d.Id == "CS0260");
  }

  [Fact]
  public void OverloadGenerator_PartialOnClass_CompilesSuccessfully()
  {
    // Arrange & Act
    var (compilation, _, _) = TestSetup(
      """
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {

        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    var diagnostics = compilation.GetDiagnostics();
    Assert.Empty(diagnostics);
  }

  [Fact]
  public void OverloadGenerator_Usings_GeneratedOverloadContainsOnlyOriginalFileUsings()
  {
    // Arrange & Act
    var (c, _, overloads) = TestSetup(
      """
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {

        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Empty(c.GetDiagnostics());

    var overload = Assert.Single(overloads);

    Assert.Single(overload.Usings, u => u == "System");
  }

  [Theory]
  [InlineData("public partial", "public partial")]
  [InlineData("internal partial", "internal partial")]
  public void OverloadGenerator_ClassAccessModifier_GeneratedOverloadHasExpectedAccessModifier(
    string accessInSource,
    string expectedAccessInGenerated)
  {
    // Arrange & Act
    var (c, _, overloads) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      {{accessInSource}} class Program
      {
        public static void Main(string[] _)
        {

        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Empty(c.GetDiagnostics());

    var overload = Assert.Single(overloads);

    Assert.Equal(expectedAccessInGenerated, overload.ClassAccessModifier);
  }

  [Theory]
  [InlineData("TestProjectNamespace", "TestProjectNamespace")]
  [InlineData("TestProject.Namespace", "TestProject.Namespace")]
  [InlineData("TestProject.Namespace.SubNamespace", "TestProject.Namespace.SubNamespace")]
  public void OverloadGenerator_Namespace_IsCorrect(
    string namespaceInSource,
    string expectedNamespace)
  {
    // Arrange & Act
    var (c, _, overloads) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace {{namespaceInSource}};

      public partial class Program
      {
        public static void Main(string[] _)
        {

        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Empty(c.GetDiagnostics());

    var overload = Assert.Single(overloads);

    Assert.Equal(expectedNamespace, overload.NamespaceName);
  }

  [Theory]
  [InlineData("Program", "Program")]
  [InlineData("Program1", "Program1")]
  [InlineData("Program2", "Program2")]
  public void OverloadGenerator_ClassName_IsCorrect(
    string classNameInSource,
    string expectedClassName)
  {
    // Arrange & Act
    var (c, _, overloads) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class {{classNameInSource}}
      {
        public static void Main(string[] _)
        {

        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Empty(c.GetDiagnostics());

    var overload = Assert.Single(overloads);

    Assert.Equal(expectedClassName, overload.ClassName);
  }

  [Theory]
  [InlineData("public static", "public static")]
  [InlineData("public", "public")]
  [InlineData("private", "private")]
  [InlineData("protected", "protected")]
  [InlineData("internal", "internal")]
  [InlineData("protected internal", "protected internal")]
  [InlineData("private protected", "private protected")]
  public void OverloadGenerator_MethodAccessModifier_IsCorrect(
    string accessInSource,
    string expectedAccessInGenerated)
  {
    // Arrange & Act
    var (c, _, overloads) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {

        }

        {{accessInSource}} void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Empty(c.GetDiagnostics());

    var overload = Assert.Single(overloads);

    Assert.Equal(expectedAccessInGenerated, overload.MethodAccessModifier);
  }

  [Theory]
  [InlineData("int", "int")]
  [InlineData("string", "string")]
  [InlineData("bool", "bool")]
  [InlineData("void", "void")]
  [InlineData("Program", "Program")]
  public void OverloadGenerator_ReturnType_IsCorrect(
    string returnTypeInSource,
    string expectedReturnType)
  {
    // Arrange & Act
    var (c, _, overloads) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {

        }

        public static {{returnTypeInSource}} Test(InspecTree<Func<int, int>> insp)
        {
          {{(returnTypeInSource == "void" ? "return;" : "return default;")}}
        }
      }
      """);

    // Assert
    Assert.Empty(c.GetDiagnostics());

    var overload = Assert.Single(overloads);

    Assert.Equal(expectedReturnType, overload.ReturnType);
  }

  [Theory]
  [InlineData("Test", "Test")]
  [InlineData("Test1", "Test1")]
  [InlineData("Test2", "Test2")]
  public void OverloadGenerator_MethodName_IsCorrect(
    string methodNameInSource,
    string expectedMethodName)
  {
    // Arrange & Act
    var (c, _, overloads) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {

        }

        public static void {{methodNameInSource}}(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Empty(c.GetDiagnostics());

    var overload = Assert.Single(overloads);

    Assert.Equal(expectedMethodName, overload.MethodName);
  }

  [Fact]
  public void OverloadGenerator_NoMethodHasInspecTreeParameters_NoGeneratedOverloads()
  {
    // Arrange & Act
    var (c, _, overloads) = TestSetup(
      """
      using System;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {

        }

        public static void Test(Func<int, int> insp)
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Empty(c.GetDiagnostics());
    Assert.Empty(overloads);
  }

  [Fact]
  public void OverloadGenerator_OneMethodHasInspecTreeParameter_OneGeneratedOverload()
  {
    // Arrange & Act
    var (c, _, overloads) = TestSetup(
      """
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {

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
    Assert.Empty(c.GetDiagnostics());
    Assert.Single(overloads);
  }

  [Fact]
  public void OverloadGenerator_MultipleMethodsHaveInspecTreeParameters_MultipleGeneratedOverloads()
  {
    // Arrange & Act
    var (c, _, overloads) = TestSetup(
      """
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {

        }

        public static void Test(InspecTree<Func<int, int>> insp)
        {
          return;
        }

        public static void Test2(InspecTree<Func<int, int>> insp)
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Empty(c.GetDiagnostics());
    Assert.Equal(2, overloads.Count);
  }

  [Theory]
  [InlineData("InspecTree<Func<int, int>> insp",
    "Func<int, int>", "insp")]
  [InlineData("int x, InspecTree<Func<int, int>> insp",
    "int", "x",
    "Func<int, int>", "insp")]
  [InlineData("int x, int y, InspecTree<Func<int, int>> insp",
    "int", "x",
    "int", "y",
    "Func<int, int>", "insp")]
  [InlineData("int x, InspecTree<Func<string, string>> insp1, InspecTree<Func<int, int>> insp2",
    "int", "x",
    "Func<string, string>", "insp1",
    "Func<int, int>", "insp2")]
  public void OverloadGenerator_MethodHasInspecTreeParamters_GeneratedParametersInOverloadIsCorrect(
    string parametersInSource,
    params string[] expectedParameters)
  {
    // Arrange & Act
    var expectedParametersAsTuples = expectedParameters
      .Where((_, index) => index % 2 == 0)
      .Zip(expectedParameters.Where((_, index) => index % 2 != 0), (first, second) => (first, second))
      .ToList();

    var (c, _, overloads) = TestSetup(
      $$"""
      using System;
      using InspecTree;

      namespace TestProject;

      public partial class Program
      {
        public static void Main(string[] _)
        {

        }

        public static void Test({{parametersInSource}})
        {
          return;
        }
      }
      """);

    // Assert
    Assert.Empty(c.GetDiagnostics());

    var overload = Assert.Single(overloads);

    Assert.Equal(expectedParametersAsTuples.Count, overload.Parameters.Count);

    for (var i = 0; i < expectedParametersAsTuples.Count; i++)
    {
      var (first, second) = expectedParametersAsTuples[i];
      var actual = overload.Parameters[i];

      Assert.Equal(first, actual.ParameterType);
      Assert.Equal(second, actual.ParameterName);
    }
  }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace InspecTree.Tests;

public class SayHelloGeneratorTest
{
  [Fact]
  public async void Test1()
  {
    var source =
    @"""
    namespace InspecTree.Example;

    public partial class Program
    {
      public static void Main(string[] _)
      {
        int x = 2;
        Test(n => n * 2);
      }

      public static void Test(InspecTree<Func<int, int>> insp)
      {
        Console.WriteLine(""Hello world!"");

        var n = insp.Delegate(2);
        Console.WriteLine(n);

        return;
      }
    }
    """;

    var generator1 = new InspecTreeOverloadInterceptorGenerator();
    var generator2 = new LambdaToInspecTreeOverloadGenerator();

    var compilation = CSharpCompilation.Create("CSharpCodeGen.GenerateAssembly")
        .AddSyntaxTrees(CSharpSyntaxTree.ParseText(source))
        .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
        .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    var driver = CSharpGeneratorDriver.Create([generator1])
        .AddGenerators([generator2])
        .RunGeneratorsAndUpdateCompilation(compilation, out _, out var _);

    // Verify the generated code
    var x = driver.GetRunResult();
  }
}

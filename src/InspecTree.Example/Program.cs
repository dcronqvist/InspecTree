using System;

namespace InspecTree.Example;

public partial class Program
{
  public static void Main(string[] _)
  {
    int x = 2;
    Test(n =>
    {
      Console.WriteLine("My own stuff");
      return n * 10 * x;
    });

    Test(n => n * 3);

    Test2(prefix => $"Hello {prefix}", "world");
  }

  public static void Test(InspecTree<Func<int, int>> insp)
  {
    var n = insp.Delegate(2);
    Console.WriteLine(n);
    return;
  }

  private static int Test2(InspecTree<Func<string, string>> insp, string prefix)
  {
    var n = insp.Delegate($"Hello {prefix}");
    Console.WriteLine(n);
    return 5;
  }

  // private sealed class SyntaxWalker : CSharpSyntaxWalker
  // {
  //   public override void VisitInvocationExpression(InvocationExpressionSyntax node)
  //   {
  //     if (node.Expression is MemberAccessExpressionSyntax maes
  //         && maes.Name.Identifier.Text == "WriteLine"
  //         && maes.Expression is IdentifierNameSyntax ins
  //         && ins.Identifier.Text == "Console")
  //     {
  //       Console.WriteLine("Found call to Console.WriteLine!");
  //     }

  //     base.VisitInvocationExpression(node);
  //   }

  //   public override void VisitBinaryExpression(BinaryExpressionSyntax node)
  //   {
  //     Console.WriteLine($"Found binary expression: (({node.Left}) {node.OperatorToken} ({node.Right}))");
  //     base.VisitBinaryExpression(node);
  //   }
  // }
}

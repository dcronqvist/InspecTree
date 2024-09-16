using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace InspecTree
{
  public class InspecTree<TDelegate> where TDelegate : Delegate
  {
    public TDelegate Delegate { get; }
    public SyntaxTree SyntaxTree { get; }

    public InspecTree(TDelegate @delegate, string source)
    {
      Delegate = @delegate;
      SyntaxTree = CSharpSyntaxTree.ParseText(source);
    }
  }
}

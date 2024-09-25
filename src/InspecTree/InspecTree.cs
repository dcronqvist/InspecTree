using System;
using Microsoft.CodeAnalysis;

namespace InspecTree
{
  public class InspecTree<TDelegate> where TDelegate : Delegate
  {
    public TDelegate Delegate { get; }
    public SyntaxTree SyntaxTree { get; }
    public Compilation Compilation { get; }

    public InspecTree(
      TDelegate @delegate,
      SyntaxTree syntaxTree,
      Compilation compilation)
    {
      Delegate = @delegate;
      SyntaxTree = syntaxTree;
      Compilation = compilation;
    }
  }
}

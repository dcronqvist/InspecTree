using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace InspecTree
{
  [AttributeUsage(AttributeTargets.Method)]
  public class CaptureSyntaxAttribute : System.Attribute
  {
  }

  public class InspecTree<TDelegate> where TDelegate : Delegate
  {
    private readonly SyntaxTree _syntaxTree;
    private readonly TDelegate _delegate;

    public TDelegate Delegate => _delegate;
    public SyntaxTree SyntaxTree => _syntaxTree;

    public InspecTree(TDelegate @delegate, string source)
    {
      _delegate = @delegate;
      _syntaxTree = CSharpSyntaxTree.ParseText(source);
    }
  }
}

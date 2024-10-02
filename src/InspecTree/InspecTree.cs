using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace InspecTree
{
  public class InspecTree<TDelegate> where TDelegate : Delegate
  {
    public TDelegate Delegate { get; }
    public LambdaExpressionSyntax LambdaSyntax { get; }
    public SemanticModel SemanticModel { get; }

    public InspecTree(
      TDelegate @delegate,
      LambdaExpressionSyntax lambdaSyntax,
      SemanticModel semanticModel)
    {
      Delegate = @delegate;
      LambdaSyntax = lambdaSyntax;
      SemanticModel = semanticModel;
    }
  }
}

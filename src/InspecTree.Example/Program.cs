using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace InspecTree.Example;

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
    var blockSyntax = insp.SyntaxTree.GetRoot().DescendantNodes().OfType<BlockSyntax>().First();
    return new CSharpToGLSLTranspiler("FragColor").Transpile(blockSyntax);
  }

  private sealed class CSharpToGLSLTranspiler : CSharpSyntaxWalker
  {
    private readonly string _fragmentOutput;
    private readonly StringBuilder _glslCode = new();
    private Stack<bool> _inMain = new();
    private int _indent = 0;

    public CSharpToGLSLTranspiler(string fragmentOutput)
    {
      _fragmentOutput = fragmentOutput;
    }

    private void Indent()
    {
      _indent += 2;
      _glslCode.Append(' ', 2);
    }

    private void Unindent()
    {
      _indent -= 2;
      Erase(2);
    }

    private void EmitLine(string code)
    {
      _glslCode.Append(code);
      _glslCode.Append('\n');
      _glslCode.Append(' ', _indent);
    }

    private void Emit(string code) => _glslCode.Append(code);
    private void Erase(int count = 1) => _glslCode.Length -= count;
    private bool IsInMain() => _inMain.Count == 1;

    public string Transpile(BlockSyntax blockSyntax)
    {
      _glslCode.Clear();
      Emit("void main() ");
      _inMain.Push(true);
      Visit(blockSyntax);
      _inMain.Pop();
      return _glslCode.ToString();
    }

    public override void VisitBlock(BlockSyntax node)
    {
      EmitLine("{");
      Indent();
      foreach (var statement in node.Statements)
      {
        if (statement.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia)))
        {
          EmitLine("");
        }
        Visit(statement);
      }
      Unindent();
      EmitLine("}");
    }

    public override void VisitReturnStatement(ReturnStatementSyntax node)
    {
      if (IsInMain())
      {
        Emit($"{_fragmentOutput} = ");
        Visit(node.Expression);
        EmitLine(";");
        EmitLine("return;");
      }
      else
      {
        Emit("return ");
        Visit(node.Expression);
        EmitLine(";");
      }
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
      Visit(node.Left);
      Emit(" = ");
      Visit(node.Right);
      EmitLine(";");
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
      Emit("(");
      Visit(node.Left);
      Emit($" {node.OperatorToken.ValueText} ");
      Visit(node.Right);
      Emit(")");
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
      Emit("if (");
      Visit(node.Condition);
      Emit(") ");
      Visit(node.Statement);

      if (node.Else != null)
      {
        EmitLine("else");
        Visit(node.Else.Statement);
      }
    }

    public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
      string glslType = GetGLSLType(node.Type);
      foreach (var variable in node.Variables)
      {
        Emit($"{glslType} {variable.Identifier.Text}");
        if (variable.Initializer != null)
        {
          Emit(" = ");
          Visit(variable.Initializer.Value);
        }
        EmitLine(";");
      }
    }
    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
      var glslType = GetGLSLType(node.Type);

      Emit(glslType);
      Emit("(");

      foreach (var argument in node.ArgumentList?.Arguments ?? [])
      {
        Visit(argument.Expression);
        Emit(", ");
      }

      Erase(2);
      Emit(")");
    }

    public override void VisitLiteralExpression(LiteralExpressionSyntax node) => Emit(node.Token.ValueText);

    public override void VisitIdentifierName(IdentifierNameSyntax node) => Emit(node.Identifier.Text);

    private static string GetGLSLType(TypeSyntax type) => type.ToString() switch
    {
      "Vector4" => "vec4",
      "int" => "int",
      "float" => "float",
      _ => throw new NotSupportedException($"Type {type} is not supported.")
    };
  }
}

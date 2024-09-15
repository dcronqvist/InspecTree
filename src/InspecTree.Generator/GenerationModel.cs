using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace InspecTree.Generator
{
  public sealed class GeneratedParameter
  {
    public string ParameterType { get; }
    public string ParameterName { get; }

    public GeneratedParameter(string parameterType, string parameterName)
    {
      ParameterType = parameterType;
      ParameterName = parameterName;
    }
  }

  public sealed class GeneratedOverload
  {
    public List<string> Usings { get; }

    public string NamespaceName { get; }
    public string ClassName { get; }
    public string ClassAccessModifier { get; }

    public string MethodAccessModifier { get; }
    public bool IsStatic { get; }
    public string ReturnType { get; }
    public string MethodName { get; }

    public List<GeneratedParameter> Parameters { get; }

    public GeneratedOverload(List<string> usings, string namespaceName, string className, string classAccessModifier, string methodAccessModifier, bool isStatic, string returnType, string methodName, List<GeneratedParameter> parameters)
    {
      Usings = usings;
      NamespaceName = namespaceName;
      ClassName = className;
      ClassAccessModifier = classAccessModifier;
      MethodAccessModifier = methodAccessModifier;
      IsStatic = isStatic;
      ReturnType = returnType;
      MethodName = methodName;
      Parameters = parameters;
    }
  }

  public sealed class SourceFile
  {
    public string FileName { get; }
    public SourceText SourceText { get; }

    public SourceFile(string fileName, SourceText sourceText)
    {
      FileName = fileName;
      SourceText = sourceText;
    }
  }

  public class InterceptedInvocation
  {
    public List<string> Usings { get; }

    public string NamespaceName { get; }
    public string ClassName { get; }
    public string ClassAccessModifier { get; }

    public int Line { get; }
    public int Column { get; }
    public string FilePath { get; }

    public string MethodAccessModifier { get; }
    public string ReturnType { get; }
    public string MethodName { get; }

    public List<GeneratedParameter> Parameters { get; }
    public ArgumentListSyntax ArgumentList { get; }

    public InterceptedInvocation(
      List<string> usings,
      string namespaceName,
      string className,
      string classAccessModifier,
      int line,
      int column,
      string filePath,
      string methodAccessModifier,
      string returnType,
      string methodName,
      List<GeneratedParameter> parameters,
      ArgumentListSyntax argumentList)
    {
      Usings = usings;
      NamespaceName = namespaceName;
      ClassName = className;
      ClassAccessModifier = classAccessModifier;
      Line = line;
      Column = column;
      FilePath = filePath;
      MethodAccessModifier = methodAccessModifier;
      ReturnType = returnType;
      MethodName = methodName;
      Parameters = parameters;
      ArgumentList = argumentList;
    }
  }
}

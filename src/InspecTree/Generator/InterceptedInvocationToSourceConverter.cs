using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace InspecTree.Generator
{
  public class InterceptedInvocationToSourceConverter : IInterceptedInvocationToSourceConverter
  {
    public IEnumerable<SourceFile> ConvertToSource(IReadOnlyCollection<InterceptedInvocation> methodsToIntercept)
    {
      foreach (var method in methodsToIntercept)
      {
        var safeFilePath = method.FilePath.Replace("\\", "_").Replace("/", "_").Replace(".", "_").Replace(":", "_");
        var fileName = $"Intercepted_{method.MethodName}_{safeFilePath}_{method.Line}_{method.Column}.g.cs";
        var conversionOfParameters = GenerateInterceptorConversionOfParameters(method, 6);
        var usings = method.Usings.Select(u => $"using {u};");
        var usingsString = string.Join("\n", usings);

        var mappedParameterNames = GetMappedParameterNames(method.Parameters);
        var callMethod = $"{method.MethodName}({string.Join(", ", method.Parameters.Select(p => mappedParameterNames[p.ParameterName]))})";

        var source = $@"
{usingsString}

namespace {method.NamespaceName}
{{
  {method.ClassAccessModifier} class {method.ClassName}
  {{
    [System.Runtime.CompilerServices.InterceptsLocation(@""{method.FilePath}"", line: {method.Line}, character: {method.Column})]
    {method.MethodAccessModifier} {method.ReturnType} {method.MethodName}__INTERCEPTED_{safeFilePath}_{method.Line}_{method.Column}({ConvertParameters(method.Parameters)})
    {{
      {conversionOfParameters}
      {(method.ReturnType == "void" ? $"{callMethod};" : $"return {callMethod};")}
    }}
  }}
}}".Trim();

        yield return new SourceFile(fileName, SourceText.From(source, Encoding.UTF8));
      }
    }

    private string ConvertParameters(IEnumerable<GeneratedParameter> parameters) =>
      string.Join(", ", parameters.Select(ConvertParameter));

    private string ConvertParameter(GeneratedParameter parameter)
    {
      if (!parameter.ParameterType.StartsWith("InspecTree<", StringComparison.InvariantCulture))
        return $"{parameter.ParameterType} {parameter.ParameterName}";

      var typeString = parameter.ParameterType;
      var withoutInspecTree = typeString.Substring("InspecTree<".Length, typeString.Length - "InspecTree<".Length - 1);
      return $"{withoutInspecTree} {parameter.ParameterName}";
    }

    private IEnumerable<string> GetAddReferences(IEnumerable<string> usings)
    {
      foreach (var @using in usings)
      {
        var assembly = GetAssemblyByName(@using);
        if (assembly is null)
          continue;

        var location = assembly.Location;
        yield return $"MetadataReference.CreateFromFile(@\"{location}\")";
      }
    }

    private Assembly GetAssemblyByName(string name)
    {
      return AppDomain.CurrentDomain.GetAssemblies().
             SingleOrDefault(assembly => assembly.GetName().Name == name);
    }

    private string GenerateInterceptorConversionOfParameters(InterceptedInvocation method, int indentLevel = 0)
    {
      var bodyBuilder = new StringBuilder();
      var mappedParameterNames = GetMappedParameterNames(method.Parameters);

      foreach (var parameter in method.Parameters)
      {
        var mappedParameterName = mappedParameterNames[parameter.ParameterName];

        if (!parameter.ParameterType.StartsWith("InspecTree<", StringComparison.InvariantCulture))
        {
          bodyBuilder.Append($"{new string(' ', indentLevel)}var {mappedParameterName} = {parameter.ParameterName};\n");
          continue;
        }

        var typeString = parameter.ParameterType;
        var withoutInspecTree = typeString.Substring("InspecTree<".Length, typeString.Length - "InspecTree<".Length - 1);
        var argumentSyntaxForParameter = GetArgumentSyntaxForParameter(method, parameter.ParameterName);
        var argumentString = argumentSyntaxForParameter.Expression.ToString().Replace("\"", "\"\"");
        var argumentLines = argumentString.Split('\n');

        bodyBuilder.Append($"{new string(' ', indentLevel)}var {mappedParameterName}_source = @\"\n");
        foreach (var @using in method.Usings)
        {
          bodyBuilder.Append($"{new string(' ', indentLevel)}using {@using};\n");
        }
        bodyBuilder.Append("\n");

        bodyBuilder.Append($"{new string(' ', indentLevel)}var {mappedParameterName}_lambda = {argumentLines[0]}\n");

        foreach (var line in argumentLines.Skip(1))
        {
          if (string.IsNullOrEmpty(line))
          {
            bodyBuilder.Append("\n");
            continue;
          }

          bodyBuilder.Append($"{new string(' ', indentLevel)}{line}\n");
        }
        bodyBuilder.Append($"{new string(' ', indentLevel)};\";\n");

        bodyBuilder.Append($"{new string(' ', indentLevel)}var {mappedParameterName}_syntaxTree = CSharpSyntaxTree.ParseText({mappedParameterName}_source);\n");
        bodyBuilder.Append($"{new string(' ', indentLevel)}var {mappedParameterName}_lambdaDeclaration = {mappedParameterName}_syntaxTree.GetRoot().DescendantNodes().OfType<VariableDeclarationSyntax>().Single(x =>\n");
        bodyBuilder.Append($"{new string(' ', indentLevel + 2)}x.Variables.Single().Identifier.Text == \"{mappedParameterName}_lambda\");\n");
        bodyBuilder.Append($"{new string(' ', indentLevel)}var {mappedParameterName}_lambdaExpression = {mappedParameterName}_lambdaDeclaration.DescendantNodes().OfType<LambdaExpressionSyntax>().Single();\n");

        bodyBuilder.Append($"{new string(' ', indentLevel)}var {mappedParameterName}_compilation = CSharpCompilation.Create(\"{mappedParameterName}\")\n");
        bodyBuilder.Append($"{new string(' ', indentLevel + 2)}.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))\n");
        var addReferences = GetAddReferences(method.Usings);
        foreach (var reference in addReferences)
        {
          bodyBuilder.Append($"{new string(' ', indentLevel + 2)}.AddReferences({reference})\n");
        }
        bodyBuilder.Append($"{new string(' ', indentLevel + 2)}.AddSyntaxTrees({mappedParameterName}_syntaxTree);\n");
        bodyBuilder.Append($"{new string(' ', indentLevel)}var {mappedParameterName}_semanticModel = {mappedParameterName}_compilation.GetSemanticModel({mappedParameterName}_syntaxTree);\n");

        bodyBuilder.Append($"{new string(' ', indentLevel)}var {mappedParameterName} = new InspecTree<{withoutInspecTree}>(\n");

        bodyBuilder.Append($"{new string(' ', indentLevel + 2)}{parameter.ParameterName},\n");
        bodyBuilder.Append($"{new string(' ', indentLevel + 2)}{mappedParameterName}_lambdaExpression,\n");
        bodyBuilder.Append($"{new string(' ', indentLevel + 2)}{mappedParameterName}_semanticModel);");
      }

      return bodyBuilder.ToString().Trim();
    }

    private IReadOnlyDictionary<string, string> GetMappedParameterNames(IEnumerable<GeneratedParameter> parameters) =>
      parameters.ToDictionary(p => p.ParameterName, p => $"overload_{p.ParameterName}");

    private ArgumentSyntax GetArgumentSyntaxForParameter(InterceptedInvocation method, string parameterName)
    {
      var indexOfParameter = method.Parameters.Select(p => p.ParameterName).ToList().IndexOf(parameterName);
      return method.ArgumentList.Arguments.ToList()[indexOfParameter];
    }
  }
}

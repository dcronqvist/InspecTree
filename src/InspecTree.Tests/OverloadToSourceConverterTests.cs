using InspecTree.Generator;

namespace InspecTree.Tests;

public class OverloadToSourceConverterTests
{
  [Fact]
  public void ConvertToSource_Always_GeneratesOneFilePerOverloadWithCorrectName()
  {
    // Arrange
    var overload1 = new GeneratedOverload(
      usings: ["System"],
      namespaceName: "TestingNamespace",
      className: "TestingClass",
      classAccessModifier: "public",
      methodAccessModifier: "public",
      isStatic: false,
      returnType: "void",
      methodName: "TestMethod",
      parameters: [
        new GeneratedParameter(
          parameterType: "Func<int, int>",
          parameterName: "func"
        )
      ]
    );
    var overload2 = new GeneratedOverload(
      usings: ["System", "System.Linq"],
      namespaceName: "TestingNamespace",
      className: "TestingClass2",
      classAccessModifier: "public",
      methodAccessModifier: "public",
      isStatic: false,
      returnType: "void",
      methodName: "TestMethod2",
      parameters: [
        new GeneratedParameter(
          parameterType: "Func<int, int>",
          parameterName: "func"
        )
      ]
    );
    var converter = new OverloadToSourceConverter();

    // Act
    var result = converter.ConvertToSource([overload1, overload2]).ToList();

    // Assert
    Assert.Equal(2, result.Count);
    Assert.Equal("TestingNamespace_TestingClass_TestMethod_Overload.g.cs", result[0].FileName);
    Assert.Equal("TestingNamespace_TestingClass2_TestMethod2_Overload.g.cs", result[1].FileName);
  }

  [Fact]
  public void ConvertToSource_NotStaticSingleParameter_GeneratesCorrectSource()
  {
    // Arrange
    var overload = new GeneratedOverload(
      usings: ["System"],
      namespaceName: "TestingNamespace",
      className: "TestingClass",
      classAccessModifier: "public",
      methodAccessModifier: "private",
      isStatic: false,
      returnType: "void",
      methodName: "TestMethod",
      parameters: [
        new GeneratedParameter(
          parameterType: "Func<int, int>",
          parameterName: "func"
        )
      ]
    );
    var converter = new OverloadToSourceConverter();

    // Act
    var result = converter.ConvertToSource(new[] { overload }).ToList();

    // Assert
    var file = Assert.Single(result);

    Assert.Equal(
      """
      using System;

      namespace TestingNamespace
      {
        public partial class TestingClass
        {
          private void TestMethod(Func<int, int> func)
          {
            /* Empty overload that can be intercepted */
            return;
          }
        }
      }
      """, file.SourceText.ToString());
  }

  [Fact]
  public void ConvertToSource_StaticMultipleParameters_GeneratesCorrectSource()
  {
    // Arrange
    var overload = new GeneratedOverload(
      usings: ["System", "System.Linq"],
      namespaceName: "TestingNamespace",
      className: "TestingClass",
      classAccessModifier: "internal",
      methodAccessModifier: "private",
      isStatic: true,
      returnType: "void",
      methodName: "TestMethod",
      parameters: [
        new GeneratedParameter(
          parameterType: "Func<int, int>",
          parameterName: "func"
        ),
        new GeneratedParameter(
          parameterType: "string",
          parameterName: "str"
        )
      ]
    );
    var converter = new OverloadToSourceConverter();

    // Act
    var result = converter.ConvertToSource(new[] { overload }).ToList();

    // Assert
    var file = Assert.Single(result);

    Assert.Equal(
      """
      using System;
      using System.Linq;

      namespace TestingNamespace
      {
        internal partial class TestingClass
        {
          private static void TestMethod(Func<int, int> func, string str)
          {
            /* Empty overload that can be intercepted */
            return;
          }
        }
      }
      """, file.SourceText.ToString());
  }

  [Fact]
  public void ConvertToSource_ReturnTypeIsNotVoid_GeneratesCorrectSource()
  {
    // Arrange
    var overload = new GeneratedOverload(
      usings: ["System"],
      namespaceName: "TestingNamespace",
      className: "TestingClass",
      classAccessModifier: "internal",
      methodAccessModifier: "private",
      isStatic: true,
      returnType: "int",
      methodName: "TestMethod",
      parameters: [
        new GeneratedParameter(
          parameterType: "Func<int, int>",
          parameterName: "func"
        ),
        new GeneratedParameter(
          parameterType: "string",
          parameterName: "str"
        )
      ]
    );
    var converter = new OverloadToSourceConverter();

    // Act
    var result = converter.ConvertToSource(new[] { overload }).ToList();

    // Assert
    var file = Assert.Single(result);

    Assert.Equal(
      """
      using System;

      namespace TestingNamespace
      {
        internal partial class TestingClass
        {
          private static int TestMethod(Func<int, int> func, string str)
          {
            /* Empty overload that can be intercepted */
            return default;
          }
        }
      }
      """, file.SourceText.ToString());
  }
}

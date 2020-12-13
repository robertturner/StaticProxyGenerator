using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StaticProxyGenerator
{
    [Generator]
    public class ProxySourceGenerator : ISourceGenerator
    {
        class IfceSyntaxReceiver : ISyntaxReceiver
        {
            public List<InterfaceDeclarationSyntax> CandidateFields { get; } = new List<InterfaceDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is InterfaceDeclarationSyntax ifceDeclarationSyntax && ifceDeclarationSyntax.AttributeLists.Count > 0)
                    CandidateFields.Add(ifceDeclarationSyntax);
            }
        }

        public static bool ImplementMethodsBothExplicitAndClassPublic = false;

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new IfceSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not IfceSyntaxReceiver receiver)
                return;

            var comp = context.Compilation;
            var assy = comp.Assembly;

            foreach (InterfaceDeclarationSyntax ifce in receiver.CandidateFields)
            {
                int? numGenArgs = null;
                if (ifce.TypeParameterList != null)
                    numGenArgs = ifce.TypeParameterList.Parameters.Count;
                var nsDecl = ifce.SyntaxTree.GetRoot().ChildNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
                if (nsDecl == null)
                    throw new ArgumentException("Missing namespace declaration");
                var ns = nsDecl.Name;
                var declType = numGenArgs.HasValue ? assy.GetTypeByMetadataName($"{ns}.{ifce.Identifier}`{numGenArgs}") : assy.GetTypeByMetadataName($"{ns}.{ifce.Identifier}");
                var attrs = declType.GetAttributes();

                if (attrs.Where(a => a.AttributeClass.ToString() == "StaticProxyInterfaces.StaticProxyGenerateAttribute").Any())
                {
                    var className = ifce.Identifier + "Implementation";

                    var extraIfces = new List<INamedTypeSymbol>();
                    foreach (var attr in attrs)
                    {
                        if (attr.AttributeClass.ToString() != "StaticProxyInterfaces.StaticProxyGenerateAttribute")
                            continue;
                        var ctorArgs = attr.ConstructorArguments;
                        foreach (var arg in ctorArgs)
                        {
                            var vals = arg.Values;
                            foreach (var v in vals)
                            {
                                var b = v.Value;
                                if (b is INamedTypeSymbol ifceType && ifceType.TypeKind == TypeKind.Interface)
                                    extraIfces.Add(ifceType);
                            }
                        }
                    }

                    var ifces = declType.AllInterfaces.Concat(extraIfces).ToArray();
                    var allIfces = new List<INamedTypeSymbol> { declType };
                    allIfces.AddRange(ifces);
                    var ifceMembers = allIfces.Select(i => new { Ifce = i, Members = i.GetMembers().Where(m => m.Kind == SymbolKind.Method).Select(m => (IMethodSymbol)m).ToArray() }).ToArray();
#if false
                    var ifceMappingsIdxes = new Dictionary<string, int>(ifces.Length + 1);
                    var members = declType.GetMembers();
                    var allMembers = members.Concat(ifces.SelectMany(ifce => ifce.GetMembers()));
                    var allMethods = allMembers.Where(m => m.Kind == SymbolKind.Method).Select(m => (IMethodSymbol)m).ToArray();
                    var ifceMappingsList = new List<string>(ifceMappingsIdxes.Count);
                    foreach (var m in allMethods)
                    {
                        //var contTypeStr = m.ContainingType.IsGenericType ? m.ContainingType.ConstructUnboundGenericType().ToString() : m.ContainingType.ToString();
                        var contTypeStr = m.ContainingType.ToString();
                        if (!ifceMappingsIdxes.ContainsKey(contTypeStr))
                        {
                            ifceMappingsIdxes.Add(contTypeStr, ifceMappingsList.Count);
                            ifceMappingsList.Add(contTypeStr);
                        }
                    }
                    var ifceMappingsText = string.Join(", ", ifceMappingsList.Select(ifceName => $"typeof({className}{ifce.TypeParameterList}).GetInterfaceMap(typeof({ifceName}))"));
                    var openTypeParamList = (ifce.TypeParameterList != null && ifce.TypeParameterList.Parameters.Count > 0) ? $"<{new string(',', ifce.TypeParameterList.Parameters.Count - 1)}>" : "";
#endif
                    var extraIfcesDecl = string.Join(", ", extraIfces.Select(ifce => $"{ifce.ContainingNamespace}.{ifce.Name}"));
                    if (!string.IsNullOrEmpty(extraIfcesDecl))
                        extraIfcesDecl = ", " + extraIfcesDecl;


                    var ifceMethodsDecl = string.Join(Environment.NewLine + ", ", ifceMembers.Select(im =>
                    {
                        var genDecl = im.Ifce.IsGenericType ? $"<{string.Join(", ", im.Ifce.TypeArguments.Select(t => t.ToString()))}>" : "";
                        return $"typeof({im.Ifce.Name}{genDecl}).GetMethods()";
                    }));

                    var implementationBuilder = new StringBuilder();
                    foreach (var node in ifce.SyntaxTree.GetRoot().ChildNodes().OfType<UsingDirectiveSyntax>())
                        implementationBuilder.AppendLine(node.ToString());
                    implementationBuilder.Append(@$"
using System.Linq;

namespace {ns}
{{
    ///
    /// Auto-generated implementation for {ifce.Identifier}
    ///
    public sealed class {className}{ifce.TypeParameterList}: {ifce.Identifier}{ifce.TypeParameterList}{extraIfcesDecl} {ifce.ConstraintClauses}
    {{
        static readonly System.Reflection.MethodInfo[][] ifceMethods = new []
        {{
            {ifceMethodsDecl}
        }};

        private readonly StaticProxyInterfaces.InterceptorHandler interceptorHandler;

        public {className}(StaticProxyInterfaces.InterceptorHandler interceptorHandler)
        {{
            if (interceptorHandler == null)
                throw new System.ArgumentNullException(nameof(interceptorHandler));
            this.interceptorHandler = interceptorHandler;
        }}
");
#if true
                    for (int i = 0; i < ifceMembers.Length; ++i)
                    {
                        for (var im = 0; im < ifceMembers[i].Members.Length; ++im)
                        {
                            var m = ifceMembers[i].Members[im];
                            var genericDef = m.IsGenericMethod ? $"<{string.Join(", ", m.TypeParameters.Select(t => t.Name.ToString()))}>" : "";
                            var genericParametersList = m.IsGenericMethod ? string.Join(", ", m.TypeParameters.Select(t => $"typeof({t.Name})")) : "";

                            var parametersDeclList = string.Join(", ", m.Parameters.Select(p => $"{p.Type} {p.Name}"));
                            var parametersArray = string.Join(", ", m.Parameters.Select(p => p.Name.ToString()));

                            var returnLine = m.ReturnsVoid ? "" : $"return ({m.ReturnType})";

                            implementationBuilder.Append($@"
        // MethodInfo index: {i}:{im}
        {m.ReturnType} {m.ContainingType}.{m.Name}{genericDef}({parametersDeclList})
        {{
            var genericParameters = new System.Type[] {{ {genericParametersList} }};
            var args = new object[] {{ {parametersArray} }};
            {returnLine}interceptorHandler(this, ifceMethods[{i}][{im}], args, genericParameters);
        }}
");
                        }
                    }
#else
                    uint methodIndex = 0;
                    foreach (var m in allMethods)
                    {
                        var genericDef = m.IsGenericMethod ? $"<{string.Join(", ", m.TypeParameters.Select(t => t.Name.ToString()))}>" : "";
                        var genericConstraintText = m.IsGenericMethod ? string.Join(" ", m.TypeParameters.Select(t =>
                        {
                            string ret = null;
                            void appendConstraint(string str) => ret = (ret == null) ? str : (ret + ", " + str);
                            if (t.HasNotNullConstraint)
                                appendConstraint("notnull");
                            if (t.HasReferenceTypeConstraint)
                                appendConstraint("class");
                            if (t.HasValueTypeConstraint)
                                appendConstraint("struct");
                            if (t.HasUnmanagedTypeConstraint)
                                appendConstraint("unmanaged");
                            foreach (var typeConstraint in t.ConstraintTypes)
                                appendConstraint(typeConstraint.Name);
                            return (ret != null) ? $" where {t.Name} : {ret}" : "";
                        })) : "";
                        var genericParametersList = m.IsGenericMethod ? string.Join(", ", m.TypeParameters.Select(t => $"typeof({t.Name})")) : "";

                        var parametersDeclList = string.Join(", ", m.Parameters.Select(p => $"{p.Type} {p.Name}"));
                        var parametersArray = string.Join(", ", m.Parameters.Select(p => p.Name.ToString()));

                        var returnLine = m.ReturnsVoid ? "" : $"return ({m.ReturnType})";
                        var ifceReturn = m.ReturnsVoid ? "" : "return ";
                        var ifceMappingIdx = ifceMappingsIdxes[m.ContainingType.ToString()];

                        var methodSigProlog = ImplementMethodsBothExplicitAndClassPublic ? $"public {m.ReturnType} " : $"{m.ReturnType} {m.ContainingType}.";
                        var primaryMethodGenericConstraints = ImplementMethodsBothExplicitAndClassPublic ? genericConstraintText : "";

                        implementationBuilder.Append($@"
        // MethodInfo index: {methodIndex}
        {methodSigProlog}{m.Name}{genericDef}({parametersDeclList}) {primaryMethodGenericConstraints}
        {{
            if (methodInfos[{methodIndex}] == null)
            {{
                var classMethod = (System.Reflection.MethodInfo)System.Reflection.MethodBase.GetCurrentMethod();
                var index = System.Array.IndexOf(ifceMappings[{ifceMappingIdx}].TargetMethods, classMethod);
                if (index < 0)
                {{
                    var m = TypeMethods.Where(m => m.Name == classMethod.Name).ToArray();
                    //index = System.Array.IndexOf(ifceMappings[{ifceMappingIdx}].TargetMethods, TypeMethods[classMethod.Name]);
                }}
                methodInfos[{methodIndex}] = ifceMappings[{ifceMappingIdx}].InterfaceMethods[index];
            }}
            var genericParameters = new System.Type[] {{ {genericParametersList} }};
            var args = new object[] {{ {parametersArray} }};
            {returnLine}interceptorHandler(this, methodInfos[{methodIndex}], args, genericParameters);
        }}
");
                        if (ImplementMethodsBothExplicitAndClassPublic)
                            implementationBuilder.Append($@"
        {m.ReturnType} {m.ContainingType}.{m.Name}{genericDef}({parametersDeclList})
        {{
            {ifceReturn}this.{m.Name}{genericDef}({parametersArray});
        }}
");
                        methodIndex++;
                    }
#endif
                    implementationBuilder.Append(@"
    }
}
");

                    var hintName = className;
                    if (ifce.TypeParameterList != null)
                        hintName += '_' + string.Join("_", ifce.TypeParameterList.Parameters.ToString());
                    context.AddSource(hintName, SourceText.From(implementationBuilder.ToString(), Encoding.UTF8));
                }
            }
        }

    }
}

#pragma warning disable RS2008

using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Mono.TextTemplating;
using Location = Microsoft.CodeAnalysis.Location;

namespace Nogic.T4Generator
{
    [Generator]
    public class Generator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context) { }

        public void Execute(GeneratorExecutionContext context)
        {
            var t4Files = context.AdditionalFiles.Where(at => at.Path.EndsWith(".tt"));
            if (t4Files?.Any() != true)
                return;

            var generator = new TemplateGenerator();
            generator.UseInProcessCompiler();

            foreach (var file in t4Files)
            {
                var content = file.GetText(context.CancellationToken);
                if (content is null)
                    continue;

                try
                {
                    var template = generator.CompileTemplate(content.ToString());
                    if (template is null)
                    {
                        var error = generator.Errors.Cast<CompilerError>().FirstOrDefault();
                        if (error is not null)
                            context.ReportDiagnostic(CreateT4ErrorDiagnostic(error));
                        continue;
                    }

                    string sourceCode = template.Process();

                    string fileName = Path.GetFileNameWithoutExtension(file.Path);
                    context.AddSource($"{fileName}.Generated.{file.GetHashCode():x8}.cs", sourceCode);
                }
                catch (Exception ex)
                {
                    if (ex is not OperationCanceledException)
                        context.ReportDiagnostic(Diagnostic.Create(_complieError, Location.None, ex));
                }
            }

            static Diagnostic CreateT4ErrorDiagnostic(CompilerError err)
                => Diagnostic.Create(_invalidFileWarning, Location.None, err.ErrorNumber, err.ErrorText);
        }

        private static readonly DiagnosticDescriptor _complieError = new(
            "T4GEN001",
            "Couldn't parse T4 file",
            "{0}",
            "T4Generator",
            DiagnosticSeverity.Warning,
            true);

        private static readonly DiagnosticDescriptor _invalidFileWarning = new(
            "T4GEN002",
            "Couldn't parse T4 file",
            "{0}: {1}",
            "T4Generator",
            DiagnosticSeverity.Warning,
            true);
    }
}

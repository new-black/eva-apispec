using System.CommandLine;

namespace EVA.SDK.Generator.V2.Commands.Generate.Helpers;

public interface IOptionProvider
{
  IEnumerable<Option> GetAllOptions();
}
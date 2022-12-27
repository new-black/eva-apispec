using System.CommandLine;

namespace EVA.SDK.Generator.V2.Helpers;

public interface IOptionProvider
{
  IEnumerable<Option> GetAllOptions();
}
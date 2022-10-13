namespace EVA.Core.Typings.V2;

[Flags]
public enum ApiSpecFuncScope
{
  None = 0,
  Create = 1,
  Edit = 2,
  Delete = 4,
  View = 8,
  Manage = Create | Edit | Delete | View | 16
}
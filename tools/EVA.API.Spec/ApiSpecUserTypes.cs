namespace EVA.API.Spec;

[Flags]
public enum ApiSpecUserTypes
{
  None = 0,
  Employee = 1,
  Customer = 2,
  Anonymous = 4,
  Business = 8,
  System = 16 | Employee,
  // 32
  Debtor = 64,
  LimitedTrust = 256,
  Tester = 512,
  RemovedByRequest = 1024,
  Api = 2048
}
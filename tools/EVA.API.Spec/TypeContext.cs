namespace EVA.API.Spec;

[Flags]
public enum TypeContext {
  None = 0,
  Request = 1,
  Response = 2,
  Both = Request | Response
}
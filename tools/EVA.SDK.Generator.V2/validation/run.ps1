# Validate swift build
dotnet run -- generate swift `
  --out ./validation/swift/src `
  --overwrite `
  --remove inheritance deprecated-properties deprecated-services `
  --opt-optimistic-nullability `
  --opt-include-mocks `
  --opt-service-format struct `
  --assembly `
    EVA.Admin `
    EVA.Auditing `
    EVA.Authentication.OpenID `
    EVA.Blobs `
    EVA.Carrier.Paazl `
    EVA.Core* `
    EVA.GlobalBlue `
    EVA.InterSolve `
    EVA.Payment.Core `
    EVA.Payment.Adyen `
    EVA.Payment.Stripe `
    EVA.PIM `
    EVA.Privacy `
    EVA.Rituals `
    EVA.UserTasks `
    EVA.Watchtower `
    EVA.Workforce 
docker build ./validation/swift --progress plain
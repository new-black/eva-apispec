# Validate swift build
dotnet run -- generate swift --out ./validation/swift/src --overwrite --remove inheritance deprecated-properties deprecated-services --opt-optimistic-nullability --opt-include-mocks
docker build ./validation/swift --progress plain
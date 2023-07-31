import Foundation

public indirect enum IndirectOptional<T> {
  case none, some(T)
}

extension IndirectOptional: Codable where T: Codable {}
extension IndirectOptional: Equatable where T: Equatable {}
extension IndirectOptional: Hashable where T: Hashable {}
extension IndirectOptional: Sendable where T: Sendable {}

@frozen public struct EvaAnyCodable: Codable {

}

@frozen public enum JSON: Codable, Equatable, Hashable, Sendable {
  case null
}

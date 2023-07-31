import Foundation

public indirect enum IndirectOptional<T: Codable>: Codable {
  case none, some(T)
}

@frozen public struct EvaAnyCodable: Codable {

}

@frozen
public enum JSON: Codable, Equatable, Hashable, Sendable {
  case null
}

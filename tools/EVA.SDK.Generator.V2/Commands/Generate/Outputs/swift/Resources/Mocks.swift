import Foundation

public indirect enum IndirectOptional<T: Codable>: Codable {
  case none, some(T)
}

public class EvaEndpoint {
    public init(url: String) {}
}

public class EvaService<REQ: Codable, RES: Codable> {
    public init(endpoint: EvaEndpoint, name: String, path: String) {}
}

@frozen public struct EvaAnyCodable: Codable {

}
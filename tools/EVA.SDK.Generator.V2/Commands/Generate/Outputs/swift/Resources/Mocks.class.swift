import Foundation

public class EvaEndpoint {
    public init(url: String) {}
}

public class EvaService<REQ: Codable, RES: Codable> {
    public init(endpoint: EvaEndpoint, name: String, path: String) {}
}
import Foundation

public protocol EvaService {
    associatedtype Request: Encodable
    associatedtype Response: Decodable
    static var name: String { get }
    static var path: String { get }
}
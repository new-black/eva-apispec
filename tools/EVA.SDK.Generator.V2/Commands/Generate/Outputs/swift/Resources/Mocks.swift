import Foundation

@propertyWrapper
public enum IndirectOptional<T> {
    indirect case value(T?)
    public init(wrappedValue: T?) {
        fatalError()
    }
    public var wrappedValue: T? {
        fatalError()
    }
}

extension IndirectOptional: Codable where T: Codable {}
extension IndirectOptional: Equatable where T: Equatable {}
extension IndirectOptional: Hashable where T: Hashable {}
extension IndirectOptional: Sendable where T: Sendable {}

extension Date: @unchecked Sendable {}
extension UUID: @unchecked Sendable {}
extension Decimal: @unchecked Sendable {}
extension Data: @unchecked Sendable {}

@frozen
public enum JSON: Codable, Equatable, Hashable, Sendable {
  case null
}

extension DecodingError {
    static func failedToDecodeAnySchema(
        type: Any.Type,
        codingPath: [any CodingKey]
    ) -> Self {
        fatalError()
    }

    public static func verifyAtLeastOneSchemaIsNotNil(
        _ values: [Any?],
        type: Any.Type,
        codingPath: [any CodingKey]
    ) throws {

    }
}

struct EnumCodingKey: CodingKey {
    var stringValue: String
    let intValue: Int? = nil

    init(stringValue: String) {
        self.stringValue = stringValue
    }

    init?(intValue: Int) {
        return nil
    }
}

func decodeLog(_ error: Error) {
    // No-op
}

extension DecodingError {
    static func verifyAnySchema<T>(_ value: T, key: CodingKey, debugDescription: String) {
        // No-op: This function intentionally does nothing.
    }
}
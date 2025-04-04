﻿import Foundation

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

extension JSONDecoder {
    static let iso8601: JSONDecoder = .init()
    static let productDetails: JSONDecoder = .init()
}

struct ProductDetailsWrapper: Codable {
    var productDetails: ProductDetails
}

extension Sequence where Element == ProductDetailsWrapper {
    var productDetails: [ProductDetails] {
        []
    }
}

extension Dictionary where Value == ProductDetailsWrapper {
    var productDetails: [Key: ProductDetails] {
        [:]
    }
}

struct ProductDetails: Codable {}

public enum Maybe<Wrapped> {
    case null
    case some(Wrapped)
}

extension Maybe: Encodable where Wrapped: Encodable {}
extension Maybe: Decodable where Wrapped: Decodable {}
extension Maybe: Equatable where Wrapped: Equatable {}
extension Maybe: Hashable where Wrapped: Hashable {}
extension Maybe: Sendable where Wrapped: Sendable {}

extension Maybe: ExpressibleByNilLiteral {
    public init(nilLiteral: ()) {
        self = .null
    }
}

protocol Wrapper {
    associatedtype Unwrapped
    var unwrapped: Unwrapped { get }
}

struct StringWrapper: Codable, Hashable, Wrapper {
    var unwrapped: String
}

struct IntWrapper: Codable, Hashable, Wrapper {
    var unwrapped: Int
}

extension Array: Wrapper where Element: Wrapper {
    var unwrapped: [Element.Unwrapped] {
        []
    }
}

extension Maybe: Wrapper where Wrapped: Wrapper {
    var unwrapped: Maybe<Wrapped.Unwrapped> {
        .null
    }
}

extension Dictionary: Wrapper where Value: Wrapper {
    var unwrapped: [Key: Value.Unwrapped] {
        [:]
    }
}

extension EVACorePageConfig: Wrapper where T: Wrapper {
    var unwrapped: EVACorePageConfig<T.Unwrapped> {
        .init(Filter: Filter?.unwrapped, Limit: Limit, SortDirection: SortDirection, SortProperty: SortProperty, Start: Start)
    }
}

extension EVACorePageTokenConfig: Wrapper where T: Wrapper {
    var unwrapped: EVACorePageTokenConfig<T.Unwrapped> {
        .init(Filter: Filter?.unwrapped, Limit: Limit)
    }
}

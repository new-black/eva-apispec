import Foundation

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

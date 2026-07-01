FROM swift:6.3.3-jammy@sha256:215c21d87c9f2ad724f634db935c5a6370cc2abf924e7a333b142ed0ed80940c

RUN mkdir /src
WORKDIR /src

RUN swift package init --type executable
COPY ./src/ ./Sources/

RUN swift build --jobs 4 -Xswiftc -Onone
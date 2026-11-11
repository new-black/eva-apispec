FROM swift:6.4.2-jammy@sha256:e86bcb3d1aa6e2bcd54251bad4b82b1f9d825757081c058840f7483c0d164744

RUN mkdir /src
WORKDIR /src

RUN swift package init --type executable
COPY ./src/ ./Sources/

RUN swift build --jobs 4 -Xswiftc -Onone
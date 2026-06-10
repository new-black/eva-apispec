FROM swift:5.8.1-jammy@sha256:78606da7da46b8a5a6a33710134e093956da1692960c22704e95c4822ecb771e

RUN mkdir /src
WORKDIR /src

RUN swift package init --type executable
COPY ./src/ ./Sources/

RUN swift build --jobs 4 -Xswiftc -Onone
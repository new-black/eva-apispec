FROM swift:6.4.1-jammy@sha256:64945e64b1e9d16eaf23414f8bcb9b880d12705bb34ad5d23180b04ad6d28e10

RUN mkdir /src
WORKDIR /src

RUN swift package init --type executable
COPY ./src/ ./Sources/

RUN swift build --jobs 4 -Xswiftc -Onone
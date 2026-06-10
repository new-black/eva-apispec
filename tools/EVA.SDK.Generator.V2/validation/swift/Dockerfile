FROM swift:6.3.2-jammy@sha256:5f846f008aa76859377d4e9dafe2826c74dde99421bbc60ae2b98d0f1daf72cf

RUN mkdir /src
WORKDIR /src

RUN swift package init --type executable
COPY ./src/ ./Sources/

RUN swift build --jobs 4 -Xswiftc -Onone
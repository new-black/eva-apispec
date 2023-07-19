FROM node:lts-alpine
WORKDIR /src

COPY . .
RUN npm ci
RUN node ./index.js
import OASNormalize from "oas-normalize";

const oas = new OASNormalize.default("/src/src/openapi.json", { enablePaths: true });

oas
  .validate()
  .then((definition) => {
    console.log("VALID");
  })
  .catch((err) => {
    console.log(err);
    process.exit(1);
  });

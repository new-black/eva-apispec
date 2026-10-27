import OASNormalize from "oas-normalize";

const oas = new OASNormalize("/src/src/openapi.json", { enablePaths: true });

oas
  .validate()
  .then(() => {
    console.log("VALID");
  })
  .catch((err) => {
    console.log(err);
    process.exit(1);
  });

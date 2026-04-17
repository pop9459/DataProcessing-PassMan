const fs = require('fs');
const path = 'docs/PassManAPI - Full Endpoint Coverage.postman_collection.json';
try {
  JSON.parse(fs.readFileSync(path,'utf8'));
  console.log('OK');
} catch (e) {
  console.error('INVALID_JSON', e.message);
  process.exit(2);
}

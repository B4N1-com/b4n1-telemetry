'use strict';

/** A process that installs the SDK and dies: the hook must report first. */
const telemetry = require('..');

const url = process.argv[2];
telemetry.init({ app: 'crash-app', url, flushMs: 5 });

setTimeout(() => {
  throw new Error('uncaught-boom');
}, 5);

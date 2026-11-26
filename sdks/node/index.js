'use strict';

/**
 * b4n1-telemetry for Node: drop-in error reporting to a B4N1 collector.
 *
 * Zero dependencies. Core modules only, so `npm install` never compiles
 * anything and the SDK cannot break an application's dependency tree.
 *
 *   require('b4n1-telemetry').init({ app: 'api' });
 */

module.exports = require('./lib/auto');

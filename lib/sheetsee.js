/*
 * sheetsee.js — a tiny, dependency-free helper for turning a published Google
 * Sheet into an HTML table, in the spirit of https://github.com/jlord/sheetsee.js
 *
 * The original sheetsee.js relied on tabletop.js and the legacy Google Sheets
 * API, which Google retired in 2020. This version instead reads a sheet's
 * published CSV export, which is the reliable modern approach, and keeps the
 * same "spreadsheet in, table out" idea.
 *
 * Usage:
 *   Sheetsee.loadSheet({ id: '<sheet id>', gid: '<tab gid>' })
 *     .then(function (rows) {
 *       Sheetsee.makeTable(rows, document.getElementById('table'));
 *     });
 */
(function (global) {
  'use strict';

  // Build the CSV export URL for a published Google Sheet. `gid` identifies the
  // individual tab within the spreadsheet and defaults to the first tab (0).
  function csvUrl(id, gid) {
    var sheetGid = gid === undefined || gid === null || gid === '' ? '0' : gid;
    return (
      'https://docs.google.com/spreadsheets/d/' +
      encodeURIComponent(id) +
      '/export?format=csv&gid=' +
      encodeURIComponent(sheetGid)
    );
  }

  // Parse a CSV string into an array of row objects keyed by the header row.
  // Handles quoted fields, escaped quotes (""), and commas/newlines in quotes.
  function parseCsv(text) {
    var rows = [];
    var row = [];
    var field = '';
    var inQuotes = false;
    var i = 0;
    var c;

    for (i = 0; i < text.length; i++) {
      c = text[i];

      if (inQuotes) {
        if (c === '"') {
          if (text[i + 1] === '"') {
            field += '"';
            i++;
          } else {
            inQuotes = false;
          }
        } else {
          field += c;
        }
        continue;
      }

      if (c === '"') {
        inQuotes = true;
      } else if (c === ',') {
        row.push(field);
        field = '';
      } else if (c === '\n') {
        row.push(field);
        rows.push(row);
        row = [];
        field = '';
      } else if (c === '\r') {
        // Ignore; handled by the following \n (or end of input below).
      } else {
        field += c;
      }
    }

    // Flush the final field/row if the file does not end with a newline.
    if (field !== '' || row.length > 0) {
      row.push(field);
      rows.push(row);
    }

    if (rows.length === 0) {
      return [];
    }

    var headers = rows[0];
    return rows.slice(1).map(function (cells) {
      var obj = {};
      headers.forEach(function (header, index) {
        obj[header] = cells[index] === undefined ? '' : cells[index];
      });
      return obj;
    });
  }

  // Fetch and parse a published Google Sheet, resolving with an array of rows.
  function loadSheet(options) {
    options = options || {};
    if (!options.id) {
      return Promise.reject(new Error('Sheetsee.loadSheet requires an "id".'));
    }
    var url = options.url || csvUrl(options.id, options.gid);
    return fetch(url).then(function (response) {
      if (!response.ok) {
        throw new Error('Failed to load sheet (HTTP ' + response.status + ').');
      }
      return response.text();
    }).then(parseCsv);
  }

  // Filter an array of row objects, keeping rows where any value contains the
  // (case-insensitive) query string.
  function filterRows(rows, query) {
    if (!query) {
      return rows;
    }
    var needle = String(query).toLowerCase();
    return rows.filter(function (row) {
      return Object.keys(row).some(function (key) {
        return String(row[key]).toLowerCase().indexOf(needle) !== -1;
      });
    });
  }

  // Render an array of row objects into a Bootstrap-styled table inside the
  // given container element. Returns the created <table> element.
  function makeTable(rows, container) {
    if (!container) {
      throw new Error('Sheetsee.makeTable requires a container element.');
    }
    container.innerHTML = '';

    if (!rows || rows.length === 0) {
      var empty = document.createElement('p');
      empty.className = 'text-muted';
      empty.textContent = 'No rows to display.';
      container.appendChild(empty);
      return null;
    }

    var headers = Object.keys(rows[0]);
    var table = document.createElement('table');
    table.className = 'table table-striped table-bordered';

    var thead = document.createElement('thead');
    var headRow = document.createElement('tr');
    headers.forEach(function (header) {
      var th = document.createElement('th');
      th.scope = 'col';
      th.textContent = header;
      headRow.appendChild(th);
    });
    thead.appendChild(headRow);
    table.appendChild(thead);

    var tbody = document.createElement('tbody');
    rows.forEach(function (row) {
      var tr = document.createElement('tr');
      headers.forEach(function (header) {
        var td = document.createElement('td');
        td.textContent = row[header];
        tr.appendChild(td);
      });
      tbody.appendChild(tr);
    });
    table.appendChild(tbody);

    container.appendChild(table);
    return table;
  }

  var Sheetsee = {
    csvUrl: csvUrl,
    parseCsv: parseCsv,
    loadSheet: loadSheet,
    filterRows: filterRows,
    makeTable: makeTable
  };

  if (typeof module !== 'undefined' && module.exports) {
    module.exports = Sheetsee;
  } else {
    global.Sheetsee = Sheetsee;
  }
})(typeof window !== 'undefined' ? window : this);

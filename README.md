# System Monitoring Agent

## Overview

The System Monitoring Agent is a .NET windows service application for collecting and sending system metrics data, including cpu , memory usage, and OS version, to a specified API endpoint. It also retrieves local IPv4 addresses and logs relevant events.

## Features

- Collects system metrics like CPU and memory usage.
- Sends collected data to a configurable API endpoint.
- Logs events and errors using Serilog.

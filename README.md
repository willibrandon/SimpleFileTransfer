# Simple File Transfer

A simple command-line file transfer utility that allows you to send and receive files over TCP/IP.

## Building the Project

Make sure you have .NET 9.0 SDK installed, then run:

```bash
dotnet build
```

## Usage

### Receiving Files (Server Mode)

To start receiving files, run:

```bash
dotnet run receive
```

This will start a server on port 9876 that listens for incoming file transfers.

### Sending Files (Client Mode)

To send a file to another computer, run:

```bash
dotnet run send <host> <filepath>
```

Where:
- `<host>` is the IP address or hostname of the receiving computer
- `<filepath>` is the path to the file you want to send

Example:
```bash
dotnet run send 192.168.1.100 myfile.txt
```

## Features

- Simple command-line interface
- Progress bar showing transfer status
- Automatic directory creation for received files
- Error handling for common issues
- Uses TCP/IP for reliable transfer
- Supports files of any size
- Shows transfer speed and progress percentage

## Notes

- The server runs on port 9876 by default
- Press Ctrl+C to stop the server
- Make sure the port is open in your firewall if needed 
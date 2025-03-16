# Simple File Transfer

A simple command-line file transfer utility that allows you to send and receive files over TCP/IP with support for compression, encryption, directory transfers, and more.

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

If you need to receive encrypted files, provide the password:

```bash
dotnet run receive --password mysecretpassword
```

### Sending Files (Client Mode)

#### Single File Transfer

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

#### Multiple File Transfer

To send multiple files at once:

```bash
dotnet run send <host> <file1> <file2> <file3>...
```

Example:
```bash
dotnet run send 192.168.1.100 file1.txt file2.txt file3.txt
```

#### Directory Transfer

To send an entire directory and its contents:

```bash
dotnet run send <host> <directory_path>
```

Example:
```bash
dotnet run send 192.168.1.100 mydirectory
```

### Transfer Options

The following options can be added to any send command:

#### Compression

Use GZip compression:
```bash
dotnet run send 192.168.1.100 myfile.txt --compress
# or
dotnet run send 192.168.1.100 myfile.txt --gzip
```

Use Brotli compression (better compression ratio):
```bash
dotnet run send 192.168.1.100 myfile.txt --brotli
```

#### Encryption

Encrypt the data during transfer:
```bash
dotnet run send 192.168.1.100 myfile.txt --encrypt mysecretpassword
```

#### Resume Capability

Enable resume capability for interrupted transfers:
```bash
dotnet run send 192.168.1.100 myfile.txt --resume
```

To list all incomplete transfers that can be resumed:
```bash
dotnet run list-resume
```

To resume a specific transfer:
```bash
dotnet run resume <index>
```

Example:
```bash
dotnet run resume 1
```

If the transfer is encrypted, provide the password:
```bash
dotnet run resume 1 --password mysecretpassword
```

### Transfer Queue

You can queue multiple transfers to be executed sequentially:

```bash
dotnet run send 192.168.1.100 myfile.txt --queue
```

Queue management commands:

```bash
dotnet run queue-list      # List all transfers in the queue
dotnet run queue-start     # Start processing the queue
dotnet run queue-stop      # Stop processing the queue
dotnet run queue-clear     # Clear all transfers from the queue
```

## Features

- Simple command-line interface
- Progress bar showing transfer status
- Automatic directory creation for received files
- Error handling for common issues
- Uses TCP/IP for reliable transfer
- Supports files of any size
- Shows transfer speed and progress percentage
- Compression support (GZip and Brotli)
- Encryption support for secure transfers
- Directory transfers with automatic path preservation
- Multiple file transfers in a single operation
- Resume capability for interrupted transfers
- Transfer queue for sequential processing
- Hash verification to ensure data integrity

## Notes

- The server runs on port 9876 by default
- Press Ctrl+C to stop the server
- Make sure the port is open in your firewall if needed
- Received files are saved to a "downloads" directory by default
- You can combine multiple options (e.g., `--brotli --encrypt password --resume`) 
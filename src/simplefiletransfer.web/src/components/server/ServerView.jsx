import { ServerControlPanel } from './ServerControlPanel';
import { ReceivedFilesList } from './ReceivedFilesList';

export function ServerView() {
  return (
    <div className="server-view">
      <h1>File Transfer Server</h1>
      <p className="description">
        Configure and manage your file transfer server. Start the server to receive files from clients.
      </p>
      
      <div className="server-container">
        <ServerControlPanel />
        <ReceivedFilesList />
      </div>
    </div>
  );
} 
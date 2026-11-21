// The program on a port, read from netstat (Windows) and ss (Linux), so the launcher can say which program to close.
using NUnit.Framework;

namespace HNMO.Launcher.Tests;

[TestFixture]
public class PortOwnerTests
{
    [Test]
    public void NetstatNamesTheListeningProcess()
    {
        const string netstat = """

            Active Connections

              Proto  Local Address          Foreign Address        State           PID
              TCP    0.0.0.0:8080           0.0.0.0:0              LISTENING       4120
              TCP    127.0.0.1:50000        127.0.0.1:44406        ESTABLISHED     777
              TCP    0.0.0.0:44406          0.0.0.0:0              LISTENING       9876
              TCP    [::]:44406             [::]:0                 LISTENING       9876
            """;
        Assert.That(PortOwner.ListeningPidFromNetstat(netstat.Replace("\r", string.Empty), 44406), Is.EqualTo(9876));
        Assert.That(PortOwner.ListeningPidFromNetstat(netstat, 8080), Is.EqualTo(4120));
        Assert.That(PortOwner.ListeningPidFromNetstat(netstat, 4440), Is.Null, "a port that only ends the same isn't it");
        Assert.That(PortOwner.ListeningPidFromNetstat(netstat, 50000), Is.Null, "a connection isn't a listener");
    }

    [Test]
    public void ProcNetTcpGivesTheListeningSocket()
    {
        // 44406 = 0xAD76; state 0A = listening, 01 = a connection.
        const string tcp = """
              sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode
               0: 00000000:1F90 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 11111 1 0000000000000000 100 0 0 10 0
               1: 0100007F:C350 0100007F:AD76 01 00000000:00000000 00:00000000 00000000  1000        0 22222 1 0000000000000000 20 4 30 10 -1
               2: 00000000:AD76 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 33333 1 0000000000000000 100 0 0 10 0
            """;
        Assert.That(PortOwner.ListeningInode(tcp, 44406), Is.EqualTo("33333"));
        Assert.That(PortOwner.ListeningInode(tcp, 8080), Is.EqualTo("11111"));
        Assert.That(PortOwner.ListeningInode(tcp, 50000), Is.Null, "a connection isn't a listener");
    }
}

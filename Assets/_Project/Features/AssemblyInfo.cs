using System.Runtime.CompilerServices;

// テストから internal(装備中フラグの書き込みなど)を直接触れるようにする
[assembly: InternalsVisibleTo("CreativeAI.Tests.EditMode")]
[assembly: InternalsVisibleTo("CreativeAI.Tests.PlayMode")]

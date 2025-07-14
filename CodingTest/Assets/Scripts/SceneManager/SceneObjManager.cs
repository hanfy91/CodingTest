using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SceneManager
{
    /// <summary>
    /// 基于四叉树之类的空间算法加对象池能力，解决场景中大量对象的管理问题
    ///  1. 通过空间分区算法（如四叉树）来管理对象的空间位置
    ///  2. 通过对象池来管理对象的实例化和复用
    ///  3. 通过相机视野来动态加载和卸载对象
    ///  4. 通过SceneObjData来存储对象的基本信息
    ///  5. 通过SceneObjManager来管理整个场景对象的加载和卸载
    /// </summary>
    public class SceneObjManager : MonoBehaviour
    {
        private Camera m_Cam;
        private IVisibleObjectPool m_ObjectPool;
        private ISpacePartition m_SpacePartition;
        private List<int> m_ActiveObjects; // 存储当前活跃对象的ID列表
        public void Init(Camera camera, IVisibleObjectPool objectPool, ISpacePartition spacePartition)
        {
            m_Cam = camera;
            this.m_ObjectPool = objectPool;
            this.m_SpacePartition = spacePartition;
            this.m_ActiveObjects = new List<int>();
        }
        void Update()
        {
            Rect visibleRect = CalculateViewRect(m_Cam); // 根据正交相机计算视野
            var visibleIds = m_SpacePartition.QueryVisible(visibleRect);

            // 卸载不可见
            foreach (int id in m_ActiveObjects)
            {
                if (!visibleIds.Contains(id))
                {
                    var obj = m_SpacePartition.GetObject(id);
                    m_SpacePartition.RemoveObject(obj);
                    m_ObjectPool.Despawn(obj);
                }
            }

            // 加载新增
            foreach (int id in visibleIds)
            {
                if (!m_ActiveObjects.Contains(id))
                {
                    var data = m_SpacePartition.GetData(id);
                    if (!data.hasInstance)
                    {
                        var spawnObj = m_ObjectPool.Spawn(data);
                        m_SpacePartition.AddObject(id, spawnObj); // 同时可设置 hasInstance = true
                    }
                    else
                    {
                        // 如果已经有实例，可能需要更新位置等
                        var spawnObj = m_SpacePartition.GetObject(id);
                        spawnObj.transform.position = data.Position;
                        spawnObj.transform.rotation = data.Rotation;
                    }
                }
            }

            m_ActiveObjects.Clear();
            m_ActiveObjects.AddRange(visibleIds);
        }
        private Rect CalculateViewRect(object cam) { return new Rect(); }
    }

    public struct SceneObjData
    {
        public int Id { get; set; }
        public string PrefabName { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public bool hasInstance { get; set; } // 是否已有实体
    }
    /// <summary>
    ///  对象池接口
    /// </summary>
    public interface IVisibleObjectPool
    {
        GameObject Spawn(string prefabName, Vector3 position, Quaternion rotation);
        Task<GameObject> SpawnAsync(string prefabName, Vector3 position, Quaternion rotation);
        void Despawn(GameObject obj);
        GameObject Spawn(SceneObjData objData);
    }
    public interface ISpacePartition
    {
        /// <summary>
        /// 添加一个对象到空间分区中
        /// </summary>
        /// <param name="obj"></param>
        void AddObject(int id, GameObject obj);
        /// <summary>
        /// 从空间分区中移除一个对象,改变SceneObjData 的状态， 是否已有实体，方便对象池复用
        /// </summary>
        /// <param name="obj"></param>
        void RemoveObject(GameObject obj);
        List<int> QueryVisible(Rect view);
        
        void InitSpaceCells();
        SceneObjData GetData(int id);
        GameObject GetObject(int id);
    }
}

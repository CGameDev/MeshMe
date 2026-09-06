// SPDX-License-Identifier: AGPL-3.0-only
// Repair orchestration only; libigl and PMP algorithms remain unmodified.
#include <emscripten/bind.h>
#include <igl/remove_duplicate_vertices.h>
#include <igl/remove_unreferenced.h>
#include <igl/bfs_orient.h>
#include <igl/centroid.h>
#include <pmp/algorithms/hole_filling.h>
#include <map>
#include <set>
#include <cmath>

using emscripten::val;
namespace {
struct Topology {
  int boundary = 0, nonmanifold = 0, inconsistent = 0;
};
Topology topology(const Eigen::MatrixXi& F) {
  std::map<std::pair<int,int>, std::pair<int,int>> edges;
  for (int f=0; f<F.rows(); ++f) for (int e=0; e<3; ++e) {
    const int a=F(f,e), b=F(f,(e+1)%3);
    auto& edge=edges[std::minmax(a,b)];
    ++edge.first; edge.second += a<b ? 1 : -1;
  }
  Topology t;
  for (const auto& entry:edges) {
    const auto [count,direction]=entry.second;
    if(count==1) ++t.boundary;
    if(count>2) ++t.nonmanifold;
    if(count==2 && direction!=0) ++t.inconsistent;
  }
  return t;
}
val diagnostics(const Eigen::MatrixXd& V,const Eigen::MatrixXi& F) {
  const auto t=topology(F); val out=val::object();
  out.set("vertices",V.rows());out.set("triangles",F.rows());
  out.set("boundaryEdges",t.boundary);out.set("nonManifoldEdges",t.nonmanifold);
  out.set("inconsistentEdges",t.inconsistent);
  return out;
}
void extract(pmp::SurfaceMesh& mesh,Eigen::MatrixXd& V,Eigen::MatrixXi& F) {
 mesh.garbage_collection();V.resize(mesh.n_vertices(),3);F.resize(mesh.n_faces(),3);
 for(auto v:mesh.vertices())for(int j=0;j<3;++j){const double x=mesh.position(v)[j];if(!std::isfinite(x))throw std::runtime_error("Non-finite patch rejected.");V(v.idx(),j)=x;}
 for(auto face:mesh.faces()){
  int j=0;for(auto v:mesh.vertices(face)){if(j>=3)throw std::runtime_error("Non-triangular patch rejected.");F(face.idx(),j++)=v.idx();}
  if(j!=3)throw std::runtime_error("Incomplete patch rejected.");
  const Eigen::Vector3d a=V.row(F(face.idx(),1))-V.row(F(face.idx(),0)),b=V.row(F(face.idx(),2))-V.row(F(face.idx(),0));
  if(a.cross(b).squaredNorm()==0)throw std::runtime_error("Zero-area patch rejected.");
 }
}
}

val repair(val positions,val indices,double maxHoleDiameter) {
 try {
  const auto p=emscripten::convertJSArrayToNumberVector<double>(positions);
  const auto f=emscripten::convertJSArrayToNumberVector<double>(indices);
  if(p.empty()||p.size()%3||f.empty()||f.size()%3||p.size()>3000000||f.size()>3000000)
    throw std::runtime_error("Repair accepts at most one million vertices and triangles.");
  if(!std::isfinite(maxHoleDiameter)||maxHoleDiameter<0)
    throw std::runtime_error("Maximum hole diameter must be finite and nonnegative; zero leaves holes open.");
  Eigen::MatrixXd V(p.size()/3,3);std::vector<int> map(p.size()/3,-1);
  int kept=0,invalidVertices=0,invalidFaces=0,degenerate=0;
  for(size_t i=0;i<map.size();++i) {
   if(!std::isfinite(p[i*3])||!std::isfinite(p[i*3+1])||!std::isfinite(p[i*3+2])){++invalidVertices;continue;}
   map[i]=kept;for(int j=0;j<3;++j)V(kept,j)=p[i*3+j];++kept;
  }
  V.conservativeResize(kept,3);Eigen::MatrixXi F(f.size()/3,3);kept=0;
  for(size_t i=0;i<f.size()/3;++i) {
   bool valid=true;for(int j=0;j<3;++j){const double x=f[i*3+j];if(!std::isfinite(x)||x<0||x>=map.size()||std::floor(x)!=x||map[static_cast<size_t>(x)]<0){valid=false;break;}}
   if(!valid){++invalidFaces;continue;}
   for(int j=0;j<3;++j)F(kept,j)=map[static_cast<size_t>(f[i*3+j])];++kept;
  }
  F.conservativeResize(kept,3);
  if(!F.rows())throw std::runtime_error("No finite valid triangles remain after repair.");
  Eigen::MatrixXd SV;Eigen::MatrixXi SF;Eigen::VectorXi I,J;
  igl::remove_duplicate_vertices(V,F,0,SV,I,J,SF);
  const int duplicates=V.rows()-SV.rows();V=SV;F=SF;
  // Report the valid-input topology after exact welding, before corrections.
  const val before=diagnostics(V,F);
  kept=0;
  for(int i=0;i<F.rows();++i){
   const Eigen::Vector3d a=V.row(F(i,1))-V.row(F(i,0)),b=V.row(F(i,2))-V.row(F(i,0));
   if(a.cross(b).squaredNorm()==0){++degenerate;continue;}
   F.row(kept++)=F.row(i).eval();
  }
  F.conservativeResize(kept,3);
  if(!F.rows())throw std::runtime_error("No nondegenerate triangles remain after repair.");
  val unresolved=val::array();int unresolvedCount=0,flipped=0,patches=0;
  if(topology(F).nonmanifold==0){
   Eigen::MatrixXi oriented;Eigen::VectorXi components;igl::bfs_orient(F,oriented,components);
   if(topology(oriented).inconsistent==0){for(int i=0;i<F.rows();++i)if(F.row(i)!=oriented.row(i))++flipped;F=oriented;patches=components.maxCoeff()+1;}
   else unresolved.set(unresolvedCount++,"Non-orientable surface: winding left unchanged.");
  }else unresolved.set(unresolvedCount++,"Non-manifold edges: ambiguous winding left unchanged.");
  igl::remove_unreferenced(V,F,SV,SF,I,J);V=SV;F=SF;
  int holes=0,filled=0;
  try {
   pmp::SurfaceMesh mesh;
   for(int i=0;i<V.rows();++i)mesh.add_vertex(pmp::Point(V(i,0),V(i,1),V(i,2)));
   for(int i=0;i<F.rows();++i)mesh.add_triangle(pmp::Vertex(F(i,0)),pmp::Vertex(F(i,1)),pmp::Vertex(F(i,2)));
   std::set<unsigned int> visited;std::vector<pmp::Halfedge> starts;
   for(auto h:mesh.halfedges())if(mesh.is_boundary(h)&&!visited.count(h.idx())){
    ++holes;auto current=h;size_t count=0;pmp::Point lo=mesh.position(mesh.to_vertex(h)),hi=lo;
    do{visited.insert(current.idx());++count;const auto& q=mesh.position(mesh.to_vertex(current));for(int j=0;j<3;++j){lo[j]=std::min(lo[j],q[j]);hi[j]=std::max(hi[j],q[j]);}current=mesh.next_halfedge(current);}while(current!=h);
    if(maxHoleDiameter>0&&count<=64&&pmp::norm(hi-lo)<=maxHoleDiameter)starts.push_back(h);
   }
   for(auto h:starts){
    if(filled>=32){unresolved.set(unresolvedCount++,"Further small holes deferred after 32 patches.");break;}
    pmp::SurfaceMesh candidate=mesh;
    try{
     pmp::fill_hole(candidate,h);
     // Validate on a separate copy: garbage collection must not invalidate the
     // stored boundary handles in the continuing original mesh.
     auto check=candidate;Eigen::MatrixXd cv;Eigen::MatrixXi cf;extract(check,cv,cf);
     if(topology(cf).boundary==0){Eigen::Vector3d center;double volume;igl::centroid(cv,cf,center,volume);if(!std::isfinite(volume)||std::abs(volume)<1e-12)throw std::runtime_error("Closing this opening gives no enclosed volume.");}
     mesh=std::move(candidate);++filled;
    }
    catch(const std::exception& e){unresolved.set(unresolvedCount++,std::string("Hole left open: ")+e.what());}
   }
   Eigen::MatrixXd finalV;Eigen::MatrixXi finalF;extract(mesh,finalV,finalF);V=finalV;F=finalF;
  }catch(const std::exception& e){unresolved.set(unresolvedCount++,std::string("Hole processing unavailable: ")+e.what());}
  if(holes>filled)unresolved.set(unresolvedCount++,"Remaining openings were not filled. Review their size and intended purpose.");
  bool reversed=false;
  if(patches==1&&topology(F).boundary==0){Eigen::Vector3d center;double volume;igl::centroid(V,F,center,volume);if(std::isfinite(volume)&&volume<0){F=F.rowwise().reverse().eval();reversed=true;}}
  else unresolved.set(unresolvedCount++,"Absolute shell orientation left unchanged for open or multiple components, including possible nested cavities.");
  unresolved.set(unresolvedCount++,"Self-intersections require further review; consistent edge winding alone does not prove a printable solid.");
  std::vector<float> outP(V.rows()*3);std::vector<unsigned int> outF(F.rows()*3);
  for(int i=0;i<V.rows();++i)for(int j=0;j<3;++j){if(!std::isfinite(V(i,j)))throw std::runtime_error("Repair produced non-finite geometry.");outP[i*3+j]=V(i,j);}
  for(int i=0;i<F.rows();++i)for(int j=0;j<3;++j)outF[i*3+j]=F(i,j);
  val out=val::object();out.set("positions",val::global("Float32Array").new_(val(emscripten::typed_memory_view(outP.size(),outP.data()))));out.set("indices",val::global("Uint32Array").new_(val(emscripten::typed_memory_view(outF.size(),outF.data()))));
  val report=val::object();report.set("before",before);report.set("after",diagnostics(V,F));report.set("invalidVerticesRemoved",invalidVertices);report.set("invalidTrianglesRemoved",invalidFaces);report.set("duplicateVerticesMerged",duplicates);report.set("degenerateTrianglesRemoved",degenerate);report.set("trianglesReoriented",flipped);report.set("closedShellReversed",reversed);report.set("holesFound",holes);report.set("holesFilled",filled);report.set("unresolved",unresolved);out.set("report",report);return out;
 }catch(const std::exception& e){val out=val::object();out.set("error",std::string(e.what()));return out;}
}
EMSCRIPTEN_BINDINGS(meshme_repair){emscripten::function("repair",&repair);}

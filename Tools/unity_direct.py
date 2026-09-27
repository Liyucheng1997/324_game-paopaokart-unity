import socket,struct,json,sys
s=socket.create_connection(('127.0.0.1',6400),timeout=60)
def read(n):
 b=b''
 while len(b)<n:
  t=s.recv(n-len(b))
  if not t: raise ConnectionError('Unity bridge closed')
  b+=t
 return b
while read(1)!=b'\n': pass
args=json.loads(sys.argv[2]) if len(sys.argv)>2 else {}
if 'code_file' in args: args['code']=open(args.pop('code_file'),encoding='utf-8').read()
b=json.dumps({'type':sys.argv[1],'params':args}).encode()
s.sendall(struct.pack('>Q',len(b))+b)
print(read(struct.unpack('>Q',read(8))[0]).decode('utf-8'))

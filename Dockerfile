FROM ubuntu:xenial

ENV GATEWAY_REPO https://github.com/Azure/azure-iot-gateway-sdk
ENV COMMIT_ID master

ADD / /build/module

RUN \
		set -ex \
	&& \
		apt-get update && apt-get install -y \
			apt-transport-https \
			curl \
			build-essential \
			libcurl4-openssl-dev \
			git \
			cmake \
			libssl-dev \
			valgrind \
			uuid-dev \
			libglib2.0-dev \
	&& \
		sh -c 'echo "deb [arch=amd64] https://apt-mo.trafficmanager.net/repos/dotnet-release/ xenial main" > /etc/apt/sources.list.d/dotnetdev.list' \
	&& \
		apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 417A0893 \
	&& \
		apt-get update && apt-get install -y \
			dotnet-dev-1.0.1 \
	&& \
		git clone --no-checkout https://github.com/Azure/azure-iot-gateway-sdk /build/gateway \
	&& \
        git -C /build/gateway checkout ${COMMIT_ID} && git -C /build/gateway submodule update --recursive --init \
	&& \
        /build/module/bld/build.sh --skip-unittests -i /build/gateway -o /gateway
	&& \
		rm -rf /build

WORKDIR /gateway
ENTRYPOINT ["sample_gateway"]
